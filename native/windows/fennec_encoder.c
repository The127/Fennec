#include "fennec_video.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

#define WIN32_LEAN_AND_MEAN
#define COBJMACROS
#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mftransform.h>
#include <mferror.h>
#include <unknwn.h>
#include <oleauto.h>
#include <strmif.h>
#include <codecapi.h>

#pragma comment(lib, "mfplat.lib")
#pragma comment(lib, "mf.lib")
#pragma comment(lib, "mfuuid.lib")
#pragma comment(lib, "ole32.lib")

struct fennec_encoder {
    IMFTransform* mft;
    DWORD input_stream_id;
    DWORD output_stream_id;
    int width;
    int height;
    int bitrate_kbps;
    int fps;
    int mf_started;
};

// Convert RGBA to NV12
static void rgba_to_nv12(const uint8_t* rgba, int stride, int w, int h,
    uint8_t* nv12_y, int y_stride, uint8_t* nv12_uv, int uv_stride) {
    for (int y = 0; y < h; y++) {
        const uint8_t* row = rgba + y * stride;
        uint8_t* yrow = nv12_y + y * y_stride;
        for (int x = 0; x < w; x++) {
            int r = row[x * 4 + 0];
            int g = row[x * 4 + 1];
            int b = row[x * 4 + 2];
            yrow[x] = (uint8_t)((66 * r + 129 * g + 25 * b + 128) >> 8) + 16;
        }
    }
    for (int y = 0; y < h; y += 2) {
        const uint8_t* row0 = rgba + y * stride;
        const uint8_t* row1 = (y + 1 < h) ? rgba + (y + 1) * stride : row0;
        uint8_t* uvrow = nv12_uv + (y / 2) * uv_stride;
        for (int x = 0; x < w; x += 2) {
            int r = (row0[x*4+0] + row0[(x+1)*4+0] + row1[x*4+0] + row1[(x+1)*4+0]) / 4;
            int g = (row0[x*4+1] + row0[(x+1)*4+1] + row1[x*4+1] + row1[(x+1)*4+1]) / 4;
            int b = (row0[x*4+2] + row0[(x+1)*4+2] + row1[x*4+2] + row1[(x+1)*4+2]) / 4;
            uvrow[x/2*2+0] = (uint8_t)((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128; // U
            uvrow[x/2*2+1] = (uint8_t)((112 * r - 94 * g - 18 * b + 128) >> 8) + 128;  // V
        }
    }
}

// Parse Annex B NALs and deliver via callback
static void deliver_nals(const uint8_t* data, int size, int64_t pts, int is_keyframe,
    fennec_nal_callback cb, void* ud) {
    int offset = 0;
    while (offset < size) {
        int sc_len = 0;
        if (offset + 3 <= size && data[offset] == 0 && data[offset+1] == 0 && data[offset+2] == 1) {
            sc_len = 3;
        } else if (offset + 4 <= size && data[offset] == 0 && data[offset+1] == 0 &&
                   data[offset+2] == 0 && data[offset+3] == 1) {
            sc_len = 4;
        } else {
            offset++;
            continue;
        }

        int nal_start = offset + sc_len;

        int nal_end = size;
        for (int j = nal_start + 1; j < size - 2; j++) {
            if (data[j] == 0 && data[j+1] == 0 &&
                (data[j+2] == 1 || (j + 3 < size && data[j+2] == 0 && data[j+3] == 1))) {
                nal_end = j;
                break;
            }
        }

        if (nal_end > nal_start) {
            cb(data + nal_start, nal_end - nal_start, pts, is_keyframe, ud);
        }

        offset = nal_end;
    }
}

static HRESULT create_encoder_mft(fennec_encoder* enc) {
    HRESULT hr;

    MFT_REGISTER_TYPE_INFO output_type = { MFMediaType_Video, MFVideoFormat_H264 };

    IMFActivate** activates = NULL;
    UINT32 count = 0;

    // Try hardware first
    hr = MFTEnumEx(MFT_CATEGORY_VIDEO_ENCODER,
        MFT_ENUM_FLAG_HARDWARE | MFT_ENUM_FLAG_SORTANDFILTER,
        NULL, &output_type, &activates, &count);

    if (SUCCEEDED(hr) && count > 0) {
        hr = IMFActivate_ActivateObject(activates[0], &IID_IMFTransform, (void**)&enc->mft);
        for (UINT32 i = 0; i < count; i++) IMFActivate_Release(activates[i]);
        CoTaskMemFree(activates);
        if (SUCCEEDED(hr)) goto configure;
    }

    // Fallback to software
    activates = NULL;
    count = 0;
    hr = MFTEnumEx(MFT_CATEGORY_VIDEO_ENCODER,
        MFT_ENUM_FLAG_SYNCMFT | MFT_ENUM_FLAG_ASYNCMFT | MFT_ENUM_FLAG_SORTANDFILTER,
        NULL, &output_type, &activates, &count);

    if (FAILED(hr) || count == 0) return E_FAIL;

    hr = IMFActivate_ActivateObject(activates[0], &IID_IMFTransform, (void**)&enc->mft);
    for (UINT32 i = 0; i < count; i++) IMFActivate_Release(activates[i]);
    CoTaskMemFree(activates);
    if (FAILED(hr)) return hr;

configure:;
    // Output type (H.264)
    IMFMediaType* outType = NULL;
    MFCreateMediaType(&outType);
    IMFMediaType_SetGUID(outType, &MF_MT_MAJOR_TYPE, &MFMediaType_Video);
    IMFMediaType_SetGUID(outType, &MF_MT_SUBTYPE, &MFVideoFormat_H264);
    MFSetAttributeSize(outType, &MF_MT_FRAME_SIZE, enc->width, enc->height);
    MFSetAttributeRatio(outType, &MF_MT_FRAME_RATE, enc->fps, 1);
    IMFMediaType_SetUINT32(outType, &MF_MT_AVG_BITRATE, enc->bitrate_kbps * 1000);
    IMFMediaType_SetUINT32(outType, &MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    MFSetAttributeRatio(outType, &MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

    hr = IMFTransform_SetOutputType(enc->mft, 0, outType, 0);
    IMFMediaType_Release(outType);
    if (FAILED(hr)) return hr;

    // Input type (NV12)
    IMFMediaType* inType = NULL;
    MFCreateMediaType(&inType);
    IMFMediaType_SetGUID(inType, &MF_MT_MAJOR_TYPE, &MFMediaType_Video);
    IMFMediaType_SetGUID(inType, &MF_MT_SUBTYPE, &MFVideoFormat_NV12);
    MFSetAttributeSize(inType, &MF_MT_FRAME_SIZE, enc->width, enc->height);
    MFSetAttributeRatio(inType, &MF_MT_FRAME_RATE, enc->fps, 1);
    IMFMediaType_SetUINT32(inType, &MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    MFSetAttributeRatio(inType, &MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

    hr = IMFTransform_SetInputType(enc->mft, 0, inType, 0);
    IMFMediaType_Release(inType);
    if (FAILED(hr)) return hr;

    // Codec API settings
    ICodecAPI* codecApi = NULL;
    hr = IMFTransform_QueryInterface(enc->mft, &IID_ICodecAPI, (void**)&codecApi);
    if (SUCCEEDED(hr)) {
        VARIANT val;
        VariantInit(&val);

        val.vt = VT_UI4;
        val.ulVal = eAVEncCommonRateControlMode_CBR;
        ICodecAPI_SetValue(codecApi, &CODECAPI_AVEncCommonRateControlMode, &val);

        val.vt = VT_UI4;
        val.ulVal = enc->fps * 5;
        ICodecAPI_SetValue(codecApi, &CODECAPI_AVEncMPVGOPSize, &val);

        val.vt = VT_BOOL;
        val.boolVal = VARIANT_TRUE;
        ICodecAPI_SetValue(codecApi, &CODECAPI_AVLowLatencyMode, &val);

        ICodecAPI_Release(codecApi);
    }

    // Stream IDs
    hr = IMFTransform_GetStreamIDs(enc->mft, 1, &enc->input_stream_id, 1, &enc->output_stream_id);
    if (hr == E_NOTIMPL) {
        enc->input_stream_id = 0;
        enc->output_stream_id = 0;
    }

    IMFTransform_ProcessMessage(enc->mft, MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, 0);
    IMFTransform_ProcessMessage(enc->mft, MFT_MESSAGE_NOTIFY_START_OF_STREAM, 0);

    return S_OK;
}

FENNEC_API fennec_encoder* fennec_encoder_create(int width, int height, int bitrate_kbps, int fps) {
    fennec_encoder* enc = calloc(1, sizeof(fennec_encoder));
    if (!enc) return NULL;

    enc->width = width & ~1;
    enc->height = height & ~1;
    enc->bitrate_kbps = bitrate_kbps;
    enc->fps = fps;

    HRESULT hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
    if (FAILED(hr) && hr != S_FALSE && hr != RPC_E_CHANGED_MODE) {
        free(enc);
        return NULL;
    }

    hr = MFStartup(MF_VERSION, MFSTARTUP_NOSOCKET);
    if (FAILED(hr)) { free(enc); return NULL; }
    enc->mf_started = 1;

    hr = create_encoder_mft(enc);
    if (FAILED(hr)) {
        MFShutdown();
        free(enc);
        return NULL;
    }

    return enc;
}

FENNEC_API fennec_status fennec_encoder_encode_rgba(fennec_encoder* enc, const uint8_t* rgba,
    int w, int h, int64_t pts, int force_kf, fennec_nal_callback cb, void* ud) {

    if (!enc || !enc->mft) return FENNEC_ERR_ENCODE;

    int enc_w = enc->width;
    int enc_h = enc->height;
    int nv12_size = enc_w * enc_h * 3 / 2;

    IMFMediaBuffer* buffer = NULL;
    HRESULT hr = MFCreateMemoryBuffer(nv12_size, &buffer);
    if (FAILED(hr)) return FENNEC_ERR_ENCODE;

    BYTE* buf_data = NULL;
    hr = IMFMediaBuffer_Lock(buffer, &buf_data, NULL, NULL);
    if (FAILED(hr)) { IMFMediaBuffer_Release(buffer); return FENNEC_ERR_ENCODE; }

    // Use the input dimensions for conversion (handle mismatch by simple clamp)
    int conv_w = (w < enc_w) ? w : enc_w;
    int conv_h = (h < enc_h) ? h : enc_h;
    // Clear buffer first if input is smaller
    if (conv_w < enc_w || conv_h < enc_h) memset(buf_data, 0, nv12_size);

    rgba_to_nv12(rgba, w * 4, conv_w, conv_h, buf_data, enc_w, buf_data + enc_w * enc_h, enc_w);

    IMFMediaBuffer_Unlock(buffer);
    IMFMediaBuffer_SetCurrentLength(buffer, nv12_size);

    IMFSample* sample = NULL;
    MFCreateSample(&sample);
    IMFSample_AddBuffer(sample, buffer);
    IMFMediaBuffer_Release(buffer);

    int64_t mf_time = pts * 100000 / 9;
    IMFSample_SetSampleTime(sample, mf_time);
    IMFSample_SetSampleDuration(sample, (LONGLONG)(10000000LL / enc->fps));

    if (force_kf) {
        IMFSample_SetUINT32(sample, &MFSampleExtension_CleanPoint, TRUE);
    }

    hr = IMFTransform_ProcessInput(enc->mft, enc->input_stream_id, sample, 0);
    IMFSample_Release(sample);
    if (FAILED(hr)) return FENNEC_ERR_ENCODE;

    // Drain output
    for (;;) {
        MFT_OUTPUT_DATA_BUFFER output_buf = {0};
        output_buf.dwStreamID = enc->output_stream_id;

        MFT_OUTPUT_STREAM_INFO stream_info;
        hr = IMFTransform_GetOutputStreamInfo(enc->mft, enc->output_stream_id, &stream_info);
        if (FAILED(hr)) break;

        IMFSample* out_sample = NULL;
        if (!(stream_info.dwFlags & MFT_OUTPUT_STREAM_PROVIDES_SAMPLES)) {
            MFCreateSample(&out_sample);
            IMFMediaBuffer* out_buf = NULL;
            MFCreateMemoryBuffer(stream_info.cbSize ? stream_info.cbSize : 1024 * 1024, &out_buf);
            IMFSample_AddBuffer(out_sample, out_buf);
            IMFMediaBuffer_Release(out_buf);
            output_buf.pSample = out_sample;
        }

        DWORD status = 0;
        hr = IMFTransform_ProcessOutput(enc->mft, 0, 1, &output_buf, &status);

        if (hr == MF_E_TRANSFORM_NEED_MORE_INPUT) {
            if (out_sample) IMFSample_Release(out_sample);
            break;
        }

        if (SUCCEEDED(hr) && output_buf.pSample) {
            IMFMediaBuffer* out_media_buf = NULL;
            hr = IMFSample_ConvertToContiguousBuffer(output_buf.pSample, &out_media_buf);
            if (SUCCEEDED(hr)) {
                BYTE* out_data = NULL;
                DWORD out_len = 0;
                hr = IMFMediaBuffer_Lock(out_media_buf, &out_data, NULL, &out_len);
                if (SUCCEEDED(hr) && out_data && out_len > 0) {
                    int is_keyframe = 0;
                    for (int i = 0; i < (int)out_len - 4; i++) {
                        if (out_data[i] == 0 && out_data[i+1] == 0 &&
                            ((out_data[i+2] == 1) || (out_data[i+2] == 0 && out_data[i+3] == 1))) {
                            int sc = (out_data[i+2] == 1) ? 3 : 4;
                            uint8_t nal_type = out_data[i + sc] & 0x1F;
                            if (nal_type == 5 || nal_type == 7) { is_keyframe = 1; break; }
                        }
                    }
                    deliver_nals(out_data, (int)out_len, pts, is_keyframe, cb, ud);
                    IMFMediaBuffer_Unlock(out_media_buf);
                }
                IMFMediaBuffer_Release(out_media_buf);
            }
        }

        if (output_buf.pSample && !out_sample) IMFSample_Release(output_buf.pSample);
        if (out_sample) IMFSample_Release(out_sample);
        if (output_buf.pEvents) IMFCollection_Release(output_buf.pEvents);

        if (FAILED(hr)) break;
    }

    return FENNEC_OK;
}

FENNEC_API fennec_status fennec_encoder_update_size(fennec_encoder* enc, int w, int h) {
    if (!enc) return FENNEC_ERR_INIT;

    w &= ~1;
    h &= ~1;
    if (w == enc->width && h == enc->height) return FENNEC_OK;

    // Drain and release old MFT
    if (enc->mft) {
        IMFTransform_ProcessMessage(enc->mft, MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0);
        IMFTransform_ProcessMessage(enc->mft, MFT_MESSAGE_COMMAND_DRAIN, 0);
        IMFTransform_Release(enc->mft);
        enc->mft = NULL;
    }

    enc->width = w;
    enc->height = h;

    HRESULT hr = create_encoder_mft(enc);
    return SUCCEEDED(hr) ? FENNEC_OK : FENNEC_ERR_INIT;
}

FENNEC_API fennec_status fennec_encoder_update_bitrate(fennec_encoder* enc, int bitrate_kbps) {
    if (!enc) return FENNEC_ERR_INIT;

    enc->bitrate_kbps = bitrate_kbps;

    // Drain and release old MFT, recreate with new bitrate
    if (enc->mft) {
        IMFTransform_ProcessMessage(enc->mft, MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0);
        IMFTransform_ProcessMessage(enc->mft, MFT_MESSAGE_COMMAND_DRAIN, 0);
        IMFTransform_Release(enc->mft);
        enc->mft = NULL;
    }

    HRESULT hr = create_encoder_mft(enc);
    return SUCCEEDED(hr) ? FENNEC_OK : FENNEC_ERR_INIT;
}

FENNEC_API fennec_status fennec_encoder_update_fps(fennec_encoder* enc, int fps) {
    if (!enc) return FENNEC_ERR_INIT;

    enc->fps = fps;

    // Drain and release old MFT, recreate with new fps
    if (enc->mft) {
        IMFTransform_ProcessMessage(enc->mft, MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0);
        IMFTransform_ProcessMessage(enc->mft, MFT_MESSAGE_COMMAND_DRAIN, 0);
        IMFTransform_Release(enc->mft);
        enc->mft = NULL;
    }

    HRESULT hr = create_encoder_mft(enc);
    return SUCCEEDED(hr) ? FENNEC_OK : FENNEC_ERR_INIT;
}

FENNEC_API const char* fennec_encoder_get_name(fennec_encoder* enc) {
    if (!enc) return "unknown";
    return "h264_mf";
}

FENNEC_API void fennec_encoder_destroy(fennec_encoder* enc) {
    if (!enc) return;

    if (enc->mft) {
        IMFTransform_ProcessMessage(enc->mft, MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0);
        IMFTransform_Release(enc->mft);
    }

    if (enc->mf_started) MFShutdown();
    free(enc);
}
