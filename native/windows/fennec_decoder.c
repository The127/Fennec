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

#pragma comment(lib, "mfplat.lib")
#pragma comment(lib, "mf.lib")
#pragma comment(lib, "mfuuid.lib")
#pragma comment(lib, "ole32.lib")

struct fennec_decoder {
    IMFTransform* mft;
    DWORD input_stream_id;
    DWORD output_stream_id;
    int width;
    int height;
    int mf_started;
    int configured;

    // Saved SPS/PPS for reconfiguration
    uint8_t* sps;
    int sps_size;
    uint8_t* pps;
    int pps_size;
};

// Convert NV12 to RGBA
static void nv12_to_rgba(const uint8_t* y_plane, int y_stride,
    const uint8_t* uv_plane, int uv_stride,
    int w, int h, uint8_t* rgba) {
    for (int y = 0; y < h; y++) {
        const uint8_t* yrow = y_plane + y * y_stride;
        const uint8_t* uvrow = uv_plane + (y / 2) * uv_stride;
        uint8_t* dst = rgba + y * w * 4;
        for (int x = 0; x < w; x++) {
            int Y = yrow[x] - 16;
            int U = uvrow[(x / 2) * 2 + 0] - 128;
            int V = uvrow[(x / 2) * 2 + 1] - 128;
            if (Y < 0) Y = 0;

            int r = (298 * Y + 409 * V + 128) >> 8;
            int g = (298 * Y - 100 * U - 208 * V + 128) >> 8;
            int b = (298 * Y + 516 * U + 128) >> 8;

            dst[x*4+0] = (uint8_t)(r < 0 ? 0 : (r > 255 ? 255 : r));
            dst[x*4+1] = (uint8_t)(g < 0 ? 0 : (g > 255 ? 255 : g));
            dst[x*4+2] = (uint8_t)(b < 0 ? 0 : (b > 255 ? 255 : b));
            dst[x*4+3] = 255;
        }
    }
}

static HRESULT create_decoder_mft(fennec_decoder* dec) {
    HRESULT hr;

    MFT_REGISTER_TYPE_INFO input_type = { MFMediaType_Video, MFVideoFormat_H264 };

    IMFActivate** activates = NULL;
    UINT32 count = 0;

    hr = MFTEnumEx(MFT_CATEGORY_VIDEO_DECODER,
        MFT_ENUM_FLAG_SYNCMFT | MFT_ENUM_FLAG_ASYNCMFT | MFT_ENUM_FLAG_SORTANDFILTER,
        &input_type, NULL, &activates, &count);

    if (FAILED(hr) || count == 0) return E_FAIL;

    hr = IMFActivate_ActivateObject(activates[0], &IID_IMFTransform, (void**)&dec->mft);
    for (UINT32 i = 0; i < count; i++) IMFActivate_Release(activates[i]);
    CoTaskMemFree(activates);
    if (FAILED(hr)) return hr;

    // Set input type (H.264)
    IMFMediaType* inType = NULL;
    MFCreateMediaType(&inType);
    IMFMediaType_SetGUID(inType, &MF_MT_MAJOR_TYPE, &MFMediaType_Video);
    IMFMediaType_SetGUID(inType, &MF_MT_SUBTYPE, &MFVideoFormat_H264);

    hr = IMFTransform_SetInputType(dec->mft, 0, inType, 0);
    IMFMediaType_Release(inType);
    if (FAILED(hr)) return hr;

    // Set output type — enumerate available and pick NV12
    for (DWORD i = 0; ; i++) {
        IMFMediaType* avail = NULL;
        hr = IMFTransform_GetOutputAvailableType(dec->mft, 0, i, &avail);
        if (FAILED(hr)) break;

        GUID subtype;
        IMFMediaType_GetGUID(avail, &MF_MT_SUBTYPE, &subtype);
        if (IsEqualGUID(&subtype, &MFVideoFormat_NV12)) {
            hr = IMFTransform_SetOutputType(dec->mft, 0, avail, 0);
            IMFMediaType_Release(avail);
            break;
        }
        IMFMediaType_Release(avail);
    }

    // Stream IDs
    hr = IMFTransform_GetStreamIDs(dec->mft, 1, &dec->input_stream_id, 1, &dec->output_stream_id);
    if (hr == E_NOTIMPL) {
        dec->input_stream_id = 0;
        dec->output_stream_id = 0;
    }

    IMFTransform_ProcessMessage(dec->mft, MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, 0);
    IMFTransform_ProcessMessage(dec->mft, MFT_MESSAGE_NOTIFY_START_OF_STREAM, 0);

    dec->configured = 1;
    return S_OK;
}

FENNEC_API fennec_decoder* fennec_decoder_create(void) {
    fennec_decoder* dec = calloc(1, sizeof(fennec_decoder));
    if (!dec) return NULL;

    HRESULT hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
    if (FAILED(hr) && hr != S_FALSE && hr != RPC_E_CHANGED_MODE) {
        free(dec);
        return NULL;
    }

    hr = MFStartup(MF_VERSION, MFSTARTUP_NOSOCKET);
    if (FAILED(hr)) { free(dec); return NULL; }
    dec->mf_started = 1;

    hr = create_decoder_mft(dec);
    if (FAILED(hr)) {
        MFShutdown();
        free(dec);
        return NULL;
    }

    return dec;
}

static void drain_decoder_output(fennec_decoder* dec, fennec_frame_callback cb, void* ud) {
    for (;;) {
        MFT_OUTPUT_DATA_BUFFER output_buf = {0};
        output_buf.dwStreamID = dec->output_stream_id;

        MFT_OUTPUT_STREAM_INFO stream_info;
        HRESULT hr = IMFTransform_GetOutputStreamInfo(dec->mft, dec->output_stream_id, &stream_info);
        if (FAILED(hr)) break;

        IMFSample* out_sample = NULL;
        if (!(stream_info.dwFlags & MFT_OUTPUT_STREAM_PROVIDES_SAMPLES)) {
            MFCreateSample(&out_sample);
            IMFMediaBuffer* out_buf = NULL;
            MFCreateMemoryBuffer(stream_info.cbSize ? stream_info.cbSize : 4 * 1024 * 1024, &out_buf);
            IMFSample_AddBuffer(out_sample, out_buf);
            IMFMediaBuffer_Release(out_buf);
            output_buf.pSample = out_sample;
        }

        DWORD status = 0;
        hr = IMFTransform_ProcessOutput(dec->mft, 0, 1, &output_buf, &status);

        if (hr == MF_E_TRANSFORM_NEED_MORE_INPUT) {
            if (out_sample) IMFSample_Release(out_sample);
            break;
        }

        if (hr == MF_E_TRANSFORM_STREAM_CHANGE) {
            // Output type changed — re-negotiate
            if (out_sample) IMFSample_Release(out_sample);
            for (DWORD i = 0; ; i++) {
                IMFMediaType* avail = NULL;
                hr = IMFTransform_GetOutputAvailableType(dec->mft, 0, i, &avail);
                if (FAILED(hr)) break;
                GUID subtype;
                IMFMediaType_GetGUID(avail, &MF_MT_SUBTYPE, &subtype);
                if (IsEqualGUID(&subtype, &MFVideoFormat_NV12)) {
                    IMFTransform_SetOutputType(dec->mft, 0, avail, 0);

                    UINT32 w = 0, h = 0;
                    MFGetAttributeSize(avail, &MF_MT_FRAME_SIZE, &w, &h);
                    dec->width = (int)w;
                    dec->height = (int)h;

                    IMFMediaType_Release(avail);
                    break;
                }
                IMFMediaType_Release(avail);
            }
            continue; // retry output
        }

        if (SUCCEEDED(hr) && output_buf.pSample) {
            IMFMediaBuffer* out_media_buf = NULL;
            hr = IMFSample_ConvertToContiguousBuffer(output_buf.pSample, &out_media_buf);
            if (SUCCEEDED(hr) && cb) {
                BYTE* out_data = NULL;
                DWORD out_len = 0;
                hr = IMFMediaBuffer_Lock(out_media_buf, &out_data, NULL, &out_len);
                if (SUCCEEDED(hr) && out_data && dec->width > 0 && dec->height > 0) {
                    int w = dec->width;
                    int h = dec->height;
                    int y_size = w * h;

                    if ((int)out_len >= y_size * 3 / 2) {
                        int rgba_size = w * h * 4;
                        uint8_t* rgba = malloc(rgba_size);
                        if (rgba) {
                            nv12_to_rgba(out_data, w, out_data + y_size, w, w, h, rgba);
                            cb(rgba, w, h, ud);
                            free(rgba);
                        }
                    }
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
}

FENNEC_API fennec_status fennec_decoder_decode(fennec_decoder* dec, const uint8_t* nal, int size,
    fennec_frame_callback cb, void* ud) {

    if (!dec || !dec->mft || !nal || size < 1) return FENNEC_ERR_DECODE;

    uint8_t nal_type = nal[0] & 0x1F;

    // Save SPS/PPS for potential reconfiguration
    if (nal_type == 7) {
        free(dec->sps);
        dec->sps = malloc(size);
        memcpy(dec->sps, nal, size);
        dec->sps_size = size;
        // Don't return — feed to decoder
    }
    if (nal_type == 8) {
        free(dec->pps);
        dec->pps = malloc(size);
        memcpy(dec->pps, nal, size);
        dec->pps_size = size;
    }

    // Wrap NAL in Annex B format
    int annex_b_size = 4 + size;
    uint8_t* annex_b = malloc(annex_b_size);
    if (!annex_b) return FENNEC_ERR_DECODE;

    annex_b[0] = 0x00;
    annex_b[1] = 0x00;
    annex_b[2] = 0x00;
    annex_b[3] = 0x01;
    memcpy(annex_b + 4, nal, size);

    // Create input sample
    IMFMediaBuffer* buffer = NULL;
    HRESULT hr = MFCreateMemoryBuffer(annex_b_size, &buffer);
    if (FAILED(hr)) { free(annex_b); return FENNEC_ERR_DECODE; }

    BYTE* buf_data = NULL;
    IMFMediaBuffer_Lock(buffer, &buf_data, NULL, NULL);
    memcpy(buf_data, annex_b, annex_b_size);
    IMFMediaBuffer_Unlock(buffer);
    IMFMediaBuffer_SetCurrentLength(buffer, annex_b_size);
    free(annex_b);

    IMFSample* sample = NULL;
    MFCreateSample(&sample);
    IMFSample_AddBuffer(sample, buffer);
    IMFMediaBuffer_Release(buffer);

    hr = IMFTransform_ProcessInput(dec->mft, dec->input_stream_id, sample, 0);
    IMFSample_Release(sample);

    if (FAILED(hr)) return FENNEC_ERR_DECODE;

    // Try to get output
    drain_decoder_output(dec, cb, ud);

    return FENNEC_OK;
}

FENNEC_API void fennec_decoder_destroy(fennec_decoder* dec) {
    if (!dec) return;

    if (dec->mft) {
        IMFTransform_ProcessMessage(dec->mft, MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0);
        IMFTransform_Release(dec->mft);
    }

    free(dec->sps);
    free(dec->pps);

    if (dec->mf_started) MFShutdown();
    free(dec);
}
