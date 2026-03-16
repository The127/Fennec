#include "fennec_video.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

#define WIN32_LEAN_AND_MEAN
#define COBJMACROS
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mftransform.h>
#include <mferror.h>
#include <unknwn.h>
#include <oleauto.h>
#include <strmif.h>
#include <codecapi.h>
#include <shlwapi.h>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "mfplat.lib")
#pragma comment(lib, "mfreadwrite.lib")
#pragma comment(lib, "mf.lib")
#pragma comment(lib, "mfuuid.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shlwapi.lib")

// --- Capture target listing ---

typedef struct {
    fennec_capture_target* targets;
    int count;
    int capacity;
} target_list;

static void target_list_add(target_list* list, const char* id, const char* name,
    int width, int height, int is_window) {
    if (list->count >= list->capacity) {
        list->capacity = list->capacity ? list->capacity * 2 : 16;
        list->targets = realloc(list->targets, list->capacity * sizeof(fennec_capture_target));
    }
    fennec_capture_target* t = &list->targets[list->count++];
    t->id = _strdup(id);
    t->name = _strdup(name);
    t->width = width;
    t->height = height;
    t->is_window = is_window;
}

static BOOL CALLBACK enum_monitors_cb(HMONITOR hmon, HDC hdc, LPRECT rect, LPARAM lparam) {
    (void)hdc;
    target_list* list = (target_list*)lparam;

    MONITORINFOEXA mi;
    memset(&mi, 0, sizeof(mi));
    mi.cbSize = sizeof(mi);
    GetMonitorInfoA(hmon, (MONITORINFO*)&mi);

    int w = rect->right - rect->left;
    int h = rect->bottom - rect->top;

    char id[64];
    snprintf(id, sizeof(id), "display:%d", list->count);

    char name[256];
    snprintf(name, sizeof(name), "Display %d (%dx%d) %s", list->count, w, h, mi.szDevice);

    target_list_add(list, id, name, w, h, 0);
    return TRUE;
}

typedef struct {
    target_list* list;
} enum_windows_ctx;

static BOOL CALLBACK enum_windows_cb(HWND hwnd, LPARAM lparam) {
    enum_windows_ctx* ctx = (enum_windows_ctx*)lparam;

    if (!IsWindowVisible(hwnd)) return TRUE;

    char title[256];
    int len = GetWindowTextA(hwnd, title, sizeof(title));
    if (len == 0) return TRUE;

    // Skip tiny windows
    RECT rect;
    GetWindowRect(hwnd, &rect);
    int w = rect.right - rect.left;
    int h = rect.bottom - rect.top;
    if (w < 100 || h < 100) return TRUE;

    // Skip tool windows and cloaked windows (UWP hidden)
    LONG exStyle = GetWindowLongA(hwnd, GWL_EXSTYLE);
    if (exStyle & WS_EX_TOOLWINDOW) return TRUE;

    char id[64];
    snprintf(id, sizeof(id), "window:%llu", (unsigned long long)(uintptr_t)hwnd);

    char name[512];
    char className[128] = "";
    GetClassNameA(hwnd, className, sizeof(className));
    snprintf(name, sizeof(name), "%s", title);

    target_list_add(ctx->list, id, name, w, h, 1);
    return TRUE;
}

FENNEC_API int fennec_capture_list_targets(fennec_capture_target** out) {
    target_list list = {0};

    EnumDisplayMonitors(NULL, NULL, enum_monitors_cb, (LPARAM)&list);

    enum_windows_ctx ctx = { &list };
    EnumWindows(enum_windows_cb, (LPARAM)&ctx);

    *out = list.targets;
    return list.count;
}

FENNEC_API void fennec_capture_free_targets(fennec_capture_target* targets, int count) {
    if (!targets) return;
    for (int i = 0; i < count; i++) {
        free((void*)targets[i].id);
        free((void*)targets[i].name);
    }
    free(targets);
}

// --- Fused capture + encode ---

struct fennec_capture {
    // DXGI duplication
    ID3D11Device* device;
    ID3D11DeviceContext* d3d_ctx;
    IDXGIOutputDuplication* duplication;
    ID3D11Texture2D* staging_tex;
    int src_width;
    int src_height;

    // Media Foundation encoder
    IMFTransform* mft;
    DWORD mft_input_stream_id;
    DWORD mft_output_stream_id;
    int is_hw_mft;

    // Config
    int max_w;
    int max_h;
    int bitrate_kbps;
    int fps;
    char* target_id;

    // Callbacks
    fennec_nal_callback nal_cb;
    fennec_frame_callback preview_cb;
    void* user_data;

    // Capture thread
    HANDLE thread;
    volatile LONG running;
    int64_t frame_count;
    int preview_interval;
};

// Convert BGRA to NV12
static void bgra_to_nv12(const uint8_t* bgra, int stride, int w, int h,
    uint8_t* nv12_y, int y_stride, uint8_t* nv12_uv, int uv_stride) {
    for (int y = 0; y < h; y++) {
        const uint8_t* row = bgra + y * stride;
        uint8_t* yrow = nv12_y + y * y_stride;
        for (int x = 0; x < w; x++) {
            int b = row[x * 4 + 0];
            int g = row[x * 4 + 1];
            int r = row[x * 4 + 2];
            yrow[x] = (uint8_t)((66 * r + 129 * g + 25 * b + 128) >> 8) + 16;
        }
    }
    for (int y = 0; y < h; y += 2) {
        const uint8_t* row0 = bgra + y * stride;
        const uint8_t* row1 = (y + 1 < h) ? bgra + (y + 1) * stride : row0;
        uint8_t* uvrow = nv12_uv + (y / 2) * uv_stride;
        for (int x = 0; x < w; x += 2) {
            int b = (row0[x*4+0] + row0[(x+1)*4+0] + row1[x*4+0] + row1[(x+1)*4+0]) / 4;
            int g = (row0[x*4+1] + row0[(x+1)*4+1] + row1[x*4+1] + row1[(x+1)*4+1]) / 4;
            int r = (row0[x*4+2] + row0[(x+1)*4+2] + row1[x*4+2] + row1[(x+1)*4+2]) / 4;
            uvrow[x/2*2+0] = (uint8_t)((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128; // U
            uvrow[x/2*2+1] = (uint8_t)((112 * r - 94 * g - 18 * b + 128) >> 8) + 128;  // V
        }
    }
}

// Convert BGRA to RGBA for preview
static void bgra_to_rgba(const uint8_t* bgra, int stride, int w, int h, uint8_t* rgba) {
    for (int y = 0; y < h; y++) {
        const uint8_t* src = bgra + y * stride;
        uint8_t* dst = rgba + y * w * 4;
        for (int x = 0; x < w; x++) {
            dst[x*4+0] = src[x*4+2]; // R
            dst[x*4+1] = src[x*4+1]; // G
            dst[x*4+2] = src[x*4+0]; // B
            dst[x*4+3] = src[x*4+3]; // A
        }
    }
}

// Parse Annex B output from MFT and deliver NAL units via callback
static void deliver_nals(const uint8_t* data, int size, int64_t pts, int is_keyframe,
    fennec_nal_callback cb, void* ud) {
    int offset = 0;
    while (offset < size) {
        // Find start code
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

        // Find next start code
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

static HRESULT create_mf_encoder(fennec_capture* cap, int width, int height) {
    HRESULT hr;

    // Find H.264 encoder MFT — prefer hardware
    MFT_REGISTER_TYPE_INFO output_type = { MFMediaType_Video, MFVideoFormat_H264 };

    IMFActivate** activates = NULL;
    UINT32 count = 0;

    hr = MFTEnumEx(MFT_CATEGORY_VIDEO_ENCODER,
        MFT_ENUM_FLAG_HARDWARE | MFT_ENUM_FLAG_SORTANDFILTER,
        NULL, &output_type, &activates, &count);

    cap->is_hw_mft = 0;
    if (SUCCEEDED(hr) && count > 0) {
        hr = IMFActivate_ActivateObject(activates[0], &IID_IMFTransform, (void**)&cap->mft);
        if (SUCCEEDED(hr)) cap->is_hw_mft = 1;
        for (UINT32 i = 0; i < count; i++) IMFActivate_Release(activates[i]);
        CoTaskMemFree(activates);
    }

    // Fallback to software MFT
    if (!cap->mft) {
        activates = NULL;
        count = 0;
        hr = MFTEnumEx(MFT_CATEGORY_VIDEO_ENCODER,
            MFT_ENUM_FLAG_SYNCMFT | MFT_ENUM_FLAG_ASYNCMFT | MFT_ENUM_FLAG_SORTANDFILTER,
            NULL, &output_type, &activates, &count);

        if (FAILED(hr) || count == 0) return E_FAIL;

        hr = IMFActivate_ActivateObject(activates[0], &IID_IMFTransform, (void**)&cap->mft);
        for (UINT32 i = 0; i < count; i++) IMFActivate_Release(activates[i]);
        CoTaskMemFree(activates);
        if (FAILED(hr)) return hr;
    }

    // Set output type (H.264)
    IMFMediaType* mediaTypeOut = NULL;
    MFCreateMediaType(&mediaTypeOut);
    IMFMediaType_SetGUID(mediaTypeOut, &MF_MT_MAJOR_TYPE, &MFMediaType_Video);
    IMFMediaType_SetGUID(mediaTypeOut, &MF_MT_SUBTYPE, &MFVideoFormat_H264);
    MFSetAttributeSize(mediaTypeOut, &MF_MT_FRAME_SIZE, width, height);
    MFSetAttributeRatio(mediaTypeOut, &MF_MT_FRAME_RATE, cap->fps, 1);
    IMFMediaType_SetUINT32(mediaTypeOut, &MF_MT_AVG_BITRATE, cap->bitrate_kbps * 1000);
    IMFMediaType_SetUINT32(mediaTypeOut, &MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    MFSetAttributeRatio(mediaTypeOut, &MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

    hr = IMFTransform_SetOutputType(cap->mft, 0, mediaTypeOut, 0);
    IMFMediaType_Release(mediaTypeOut);
    if (FAILED(hr)) return hr;

    // Set input type (NV12)
    IMFMediaType* mediaTypeIn = NULL;
    MFCreateMediaType(&mediaTypeIn);
    IMFMediaType_SetGUID(mediaTypeIn, &MF_MT_MAJOR_TYPE, &MFMediaType_Video);
    IMFMediaType_SetGUID(mediaTypeIn, &MF_MT_SUBTYPE, &MFVideoFormat_NV12);
    MFSetAttributeSize(mediaTypeIn, &MF_MT_FRAME_SIZE, width, height);
    MFSetAttributeRatio(mediaTypeIn, &MF_MT_FRAME_RATE, cap->fps, 1);
    IMFMediaType_SetUINT32(mediaTypeIn, &MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    MFSetAttributeRatio(mediaTypeIn, &MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

    hr = IMFTransform_SetInputType(cap->mft, 0, mediaTypeIn, 0);
    IMFMediaType_Release(mediaTypeIn);
    if (FAILED(hr)) return hr;

    // Configure encoder via ICodecAPI
    ICodecAPI* codecApi = NULL;
    hr = IMFTransform_QueryInterface(cap->mft, &IID_ICodecAPI, (void**)&codecApi);
    if (SUCCEEDED(hr)) {
        VARIANT val;
        VariantInit(&val);

        // CBR rate control
        val.vt = VT_UI4;
        val.ulVal = eAVEncCommonRateControlMode_CBR;
        ICodecAPI_SetValue(codecApi, &CODECAPI_AVEncCommonRateControlMode, &val);

        // GOP size = fps * 5
        val.vt = VT_UI4;
        val.ulVal = cap->fps * 5;
        ICodecAPI_SetValue(codecApi, &CODECAPI_AVEncMPVGOPSize, &val);

        // Low latency
        val.vt = VT_BOOL;
        val.boolVal = VARIANT_TRUE;
        ICodecAPI_SetValue(codecApi, &CODECAPI_AVLowLatencyMode, &val);

        ICodecAPI_Release(codecApi);
    }

    // Get stream IDs
    hr = IMFTransform_GetStreamIDs(cap->mft, 1, &cap->mft_input_stream_id,
        1, &cap->mft_output_stream_id);
    if (hr == E_NOTIMPL) {
        cap->mft_input_stream_id = 0;
        cap->mft_output_stream_id = 0;
    }

    // Start streaming
    hr = IMFTransform_ProcessMessage(cap->mft, MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, 0);
    if (FAILED(hr)) return hr;

    hr = IMFTransform_ProcessMessage(cap->mft, MFT_MESSAGE_NOTIFY_START_OF_STREAM, 0);
    return hr;
}

static HRESULT init_dxgi_duplication(fennec_capture* cap, int display_index) {
    HRESULT hr;

    D3D_FEATURE_LEVEL featureLevel;
    hr = D3D11CreateDevice(NULL, D3D_DRIVER_TYPE_HARDWARE, NULL,
        D3D11_CREATE_DEVICE_BGRA_SUPPORT, NULL, 0,
        D3D11_SDK_VERSION, &cap->device, &featureLevel, &cap->d3d_ctx);
    if (FAILED(hr)) return hr;

    IDXGIDevice* dxgiDevice = NULL;
    hr = ID3D11Device_QueryInterface(cap->device, &IID_IDXGIDevice, (void**)&dxgiDevice);
    if (FAILED(hr)) return hr;

    IDXGIAdapter* adapter = NULL;
    hr = IDXGIDevice_GetAdapter(dxgiDevice, &adapter);
    IDXGIDevice_Release(dxgiDevice);
    if (FAILED(hr)) return hr;

    IDXGIOutput* output = NULL;
    hr = IDXGIAdapter_EnumOutputs(adapter, display_index, &output);
    IDXGIAdapter_Release(adapter);
    if (FAILED(hr)) return hr;

    DXGI_OUTPUT_DESC outputDesc;
    IDXGIOutput_GetDesc(output, &outputDesc);
    cap->src_width = outputDesc.DesktopCoordinates.right - outputDesc.DesktopCoordinates.left;
    cap->src_height = outputDesc.DesktopCoordinates.bottom - outputDesc.DesktopCoordinates.top;

    IDXGIOutput1* output1 = NULL;
    hr = IDXGIOutput_QueryInterface(output, &IID_IDXGIOutput1, (void**)&output1);
    IDXGIOutput_Release(output);
    if (FAILED(hr)) return hr;

    hr = IDXGIOutput1_DuplicateOutput(output1, (IUnknown*)cap->device, &cap->duplication);
    IDXGIOutput1_Release(output1);
    if (FAILED(hr)) return hr;

    // Create staging texture for CPU readback (preview + encode)
    D3D11_TEXTURE2D_DESC texDesc = {0};
    texDesc.Width = cap->src_width;
    texDesc.Height = cap->src_height;
    texDesc.MipLevels = 1;
    texDesc.ArraySize = 1;
    texDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    texDesc.SampleDesc.Count = 1;
    texDesc.Usage = D3D11_USAGE_STAGING;
    texDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;

    hr = ID3D11Device_CreateTexture2D(cap->device, &texDesc, NULL, &cap->staging_tex);
    return hr;
}

static HRESULT encode_frame_mf(fennec_capture* cap, const uint8_t* bgra, int stride,
    int width, int height, int64_t pts) {
    HRESULT hr;

    // Ensure even dimensions
    int enc_w = width & ~1;
    int enc_h = height & ~1;

    // Create input sample with NV12 buffer
    int nv12_size = enc_w * enc_h * 3 / 2;
    IMFMediaBuffer* buffer = NULL;
    hr = MFCreateMemoryBuffer(nv12_size, &buffer);
    if (FAILED(hr)) return hr;

    BYTE* buf_data = NULL;
    hr = IMFMediaBuffer_Lock(buffer, &buf_data, NULL, NULL);
    if (FAILED(hr)) { IMFMediaBuffer_Release(buffer); return hr; }

    // Convert BGRA to NV12
    uint8_t* y_plane = buf_data;
    uint8_t* uv_plane = buf_data + enc_w * enc_h;
    bgra_to_nv12(bgra, stride, enc_w, enc_h, y_plane, enc_w, uv_plane, enc_w);

    IMFMediaBuffer_Unlock(buffer);
    IMFMediaBuffer_SetCurrentLength(buffer, nv12_size);

    IMFSample* sample = NULL;
    MFCreateSample(&sample);
    IMFSample_AddBuffer(sample, buffer);
    IMFMediaBuffer_Release(buffer);

    // Set timestamps (100-nanosecond units)
    int64_t mf_time = pts * 100000 / 9; // convert from 90kHz to 100ns
    IMFSample_SetSampleTime(sample, mf_time);
    IMFSample_SetSampleDuration(sample, (LONGLONG)(10000000LL / cap->fps));

    hr = IMFTransform_ProcessInput(cap->mft, cap->mft_input_stream_id, sample, 0);
    IMFSample_Release(sample);
    if (FAILED(hr)) return hr;

    // Drain output
    for (;;) {
        MFT_OUTPUT_DATA_BUFFER output_buf = {0};
        output_buf.dwStreamID = cap->mft_output_stream_id;

        // Check if MFT provides its own samples
        MFT_OUTPUT_STREAM_INFO stream_info;
        hr = IMFTransform_GetOutputStreamInfo(cap->mft, cap->mft_output_stream_id, &stream_info);
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
        hr = IMFTransform_ProcessOutput(cap->mft, 0, 1, &output_buf, &status);

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
                    // Check if keyframe
                    int is_keyframe = 0;
                    // NAL type 5 = IDR
                    for (int i = 0; i < (int)out_len - 4; i++) {
                        if (out_data[i] == 0 && out_data[i+1] == 0 &&
                            ((out_data[i+2] == 1) || (out_data[i+2] == 0 && out_data[i+3] == 1))) {
                            int sc = (out_data[i+2] == 1) ? 3 : 4;
                            uint8_t nal_type = out_data[i + sc] & 0x1F;
                            if (nal_type == 5 || nal_type == 7) { is_keyframe = 1; break; }
                        }
                    }

                    deliver_nals(out_data, (int)out_len, pts, is_keyframe, cap->nal_cb, cap->user_data);
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

    return S_OK;
}

static DWORD WINAPI capture_thread_proc(LPVOID param) {
    fennec_capture* cap = (fennec_capture*)param;
    DWORD frame_interval_ms = 1000 / cap->fps;

    while (InterlockedCompareExchange(&cap->running, 1, 1) == 1) {
        IDXGIResource* desktop_resource = NULL;
        DXGI_OUTDUPL_FRAME_INFO frame_info;

        HRESULT hr = IDXGIOutputDuplication_AcquireNextFrame(
            cap->duplication, frame_interval_ms, &frame_info, &desktop_resource);

        if (hr == DXGI_ERROR_WAIT_TIMEOUT) {
            continue;
        }

        if (FAILED(hr)) {
            // Duplication lost (e.g., resolution change, secure desktop)
            // Try to release and re-acquire
            Sleep(100);
            continue;
        }

        ID3D11Texture2D* desktop_tex = NULL;
        hr = IDXGIResource_QueryInterface(desktop_resource,
            &IID_ID3D11Texture2D, (void**)&desktop_tex);
        IDXGIResource_Release(desktop_resource);

        if (FAILED(hr)) {
            IDXGIOutputDuplication_ReleaseFrame(cap->duplication);
            continue;
        }

        // Copy to staging texture
        ID3D11DeviceContext_CopyResource(cap->d3d_ctx,
            (ID3D11Resource*)cap->staging_tex, (ID3D11Resource*)desktop_tex);
        ID3D11Texture2D_Release(desktop_tex);

        // Map staging texture for CPU read
        D3D11_MAPPED_SUBRESOURCE mapped;
        hr = ID3D11DeviceContext_Map(cap->d3d_ctx,
            (ID3D11Resource*)cap->staging_tex, 0, D3D11_MAP_READ, 0, &mapped);

        if (SUCCEEDED(hr)) {
            int64_t pts = cap->frame_count * 90000 / cap->fps; // RTP timebase

            // Encode the frame
            encode_frame_mf(cap, (const uint8_t*)mapped.pData, mapped.RowPitch,
                cap->src_width, cap->src_height, pts);

            // Preview at reduced rate
            if (cap->preview_cb && (cap->frame_count % cap->preview_interval == 0)) {
                int rgba_size = cap->src_width * cap->src_height * 4;
                uint8_t* rgba = malloc(rgba_size);
                if (rgba) {
                    bgra_to_rgba((const uint8_t*)mapped.pData, mapped.RowPitch,
                        cap->src_width, cap->src_height, rgba);
                    cap->preview_cb(rgba, cap->src_width, cap->src_height, cap->user_data);
                    free(rgba);
                }
            }

            ID3D11DeviceContext_Unmap(cap->d3d_ctx,
                (ID3D11Resource*)cap->staging_tex, 0);

            cap->frame_count++;
        }

        IDXGIOutputDuplication_ReleaseFrame(cap->duplication);
    }

    return 0;
}

FENNEC_API fennec_capture* fennec_capture_create(const char* target_id, int max_w, int max_h,
    int bitrate_kbps, int fps,
    fennec_nal_callback nal_cb, fennec_frame_callback preview_cb, void* user_data) {

    fennec_capture* cap = calloc(1, sizeof(fennec_capture));
    if (!cap) return NULL;

    cap->target_id = _strdup(target_id);
    cap->max_w = max_w;
    cap->max_h = max_h;
    cap->bitrate_kbps = bitrate_kbps;
    cap->fps = fps;
    cap->nal_cb = nal_cb;
    cap->preview_cb = preview_cb;
    cap->user_data = user_data;
    cap->preview_interval = fps; // preview once per second

    return cap;
}

FENNEC_API fennec_status fennec_capture_start(fennec_capture* cap) {
    if (!cap) return FENNEC_ERR_INIT;

    HRESULT hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
    if (FAILED(hr) && hr != S_FALSE && hr != RPC_E_CHANGED_MODE) return FENNEC_ERR_INIT;

    hr = MFStartup(MF_VERSION, MFSTARTUP_NOSOCKET);
    if (FAILED(hr)) return FENNEC_ERR_INIT;

    // Parse display index from target_id
    int display_index = 0;
    if (strncmp(cap->target_id, "display:", 8) == 0) {
        display_index = atoi(cap->target_id + 8);
    }
    // For window targets, we capture the whole desktop and would crop later
    // (simplified: always capture primary display for window targets)

    hr = init_dxgi_duplication(cap, display_index);
    if (FAILED(hr)) return FENNEC_ERR_INIT;

    // Use source dimensions for encoding (clamped to max)
    int enc_w = cap->src_width;
    int enc_h = cap->src_height;
    if (enc_w > cap->max_w || enc_h > cap->max_h) {
        float scale = fminf((float)cap->max_w / enc_w, (float)cap->max_h / enc_h);
        enc_w = (int)(enc_w * scale) & ~1;
        enc_h = (int)(enc_h * scale) & ~1;
    }

    hr = create_mf_encoder(cap, enc_w, enc_h);
    if (FAILED(hr)) return FENNEC_ERR_INIT;

    InterlockedExchange(&cap->running, 1);
    cap->thread = CreateThread(NULL, 0, capture_thread_proc, cap, 0, NULL);
    if (!cap->thread) {
        InterlockedExchange(&cap->running, 0);
        return FENNEC_ERR_INIT;
    }

    return FENNEC_OK;
}

FENNEC_API fennec_status fennec_capture_stop(fennec_capture* cap) {
    if (!cap) return FENNEC_ERR_INIT;

    InterlockedExchange(&cap->running, 0);

    if (cap->thread) {
        WaitForSingleObject(cap->thread, 5000);
        CloseHandle(cap->thread);
        cap->thread = NULL;
    }

    if (cap->mft) {
        IMFTransform_ProcessMessage(cap->mft, MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0);
        IMFTransform_ProcessMessage(cap->mft, MFT_MESSAGE_COMMAND_DRAIN, 0);
    }

    return FENNEC_OK;
}

FENNEC_API fennec_status fennec_capture_update_bitrate(fennec_capture* cap, int bitrate_kbps) {
    if (!cap) return FENNEC_ERR_INIT;

    // Stop capture thread
    InterlockedExchange(&cap->running, 0);
    if (cap->thread) {
        WaitForSingleObject(cap->thread, 5000);
        CloseHandle(cap->thread);
        cap->thread = NULL;
    }

    cap->bitrate_kbps = bitrate_kbps;

    // Recreate MFT with new bitrate
    if (cap->mft) {
        IMFTransform_ProcessMessage(cap->mft, MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0);
        IMFTransform_ProcessMessage(cap->mft, MFT_MESSAGE_COMMAND_DRAIN, 0);
        IMFTransform_Release(cap->mft);
        cap->mft = NULL;
    }

    int enc_w = cap->src_width;
    int enc_h = cap->src_height;
    if (enc_w > cap->max_w || enc_h > cap->max_h) {
        float scale = fminf((float)cap->max_w / enc_w, (float)cap->max_h / enc_h);
        enc_w = (int)(enc_w * scale) & ~1;
        enc_h = (int)(enc_h * scale) & ~1;
    }

    HRESULT hr = create_mf_encoder(cap, enc_w, enc_h);
    if (FAILED(hr)) return FENNEC_ERR_INIT;

    // Restart capture thread
    InterlockedExchange(&cap->running, 1);
    cap->thread = CreateThread(NULL, 0, capture_thread_proc, cap, 0, NULL);
    if (!cap->thread) {
        InterlockedExchange(&cap->running, 0);
        return FENNEC_ERR_INIT;
    }

    return FENNEC_OK;
}

FENNEC_API fennec_status fennec_capture_update_fps(fennec_capture* cap, int fps) {
    if (!cap) return FENNEC_ERR_INIT;

    // Stop capture thread
    InterlockedExchange(&cap->running, 0);
    if (cap->thread) {
        WaitForSingleObject(cap->thread, 5000);
        CloseHandle(cap->thread);
        cap->thread = NULL;
    }

    cap->fps = fps;
    cap->preview_interval = fps; // preview once per second

    // Recreate MFT with new fps
    if (cap->mft) {
        IMFTransform_ProcessMessage(cap->mft, MFT_MESSAGE_NOTIFY_END_OF_STREAM, 0);
        IMFTransform_ProcessMessage(cap->mft, MFT_MESSAGE_COMMAND_DRAIN, 0);
        IMFTransform_Release(cap->mft);
        cap->mft = NULL;
    }

    int enc_w = cap->src_width;
    int enc_h = cap->src_height;
    if (enc_w > cap->max_w || enc_h > cap->max_h) {
        float scale = fminf((float)cap->max_w / enc_w, (float)cap->max_h / enc_h);
        enc_w = (int)(enc_w * scale) & ~1;
        enc_h = (int)(enc_h * scale) & ~1;
    }

    HRESULT hr = create_mf_encoder(cap, enc_w, enc_h);
    if (FAILED(hr)) return FENNEC_ERR_INIT;

    // Restart capture thread
    InterlockedExchange(&cap->running, 1);
    cap->thread = CreateThread(NULL, 0, capture_thread_proc, cap, 0, NULL);
    if (!cap->thread) {
        InterlockedExchange(&cap->running, 0);
        return FENNEC_ERR_INIT;
    }

    return FENNEC_OK;
}

FENNEC_API void fennec_capture_destroy(fennec_capture* cap) {
    if (!cap) return;

    fennec_capture_stop(cap);

    if (cap->mft) IMFTransform_Release(cap->mft);
    if (cap->staging_tex) ID3D11Texture2D_Release(cap->staging_tex);
    if (cap->duplication) IDXGIOutputDuplication_Release(cap->duplication);
    if (cap->d3d_ctx) ID3D11DeviceContext_Release(cap->d3d_ctx);
    if (cap->device) ID3D11Device_Release(cap->device);

    MFShutdown();

    free(cap->target_id);
    free(cap);
}
