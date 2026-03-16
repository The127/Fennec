#ifndef FENNEC_VIDEO_H
#define FENNEC_VIDEO_H

#include <stdint.h>

#ifdef _WIN32
    #ifdef FENNEC_VIDEO_EXPORTS
        #define FENNEC_API __declspec(dllexport)
    #else
        #define FENNEC_API __declspec(dllimport)
    #endif
#else
    #define FENNEC_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct fennec_encoder fennec_encoder;
typedef struct fennec_decoder fennec_decoder;
typedef struct fennec_capture fennec_capture;

typedef enum {
    FENNEC_OK = 0,
    FENNEC_ERR_INIT = -1,
    FENNEC_ERR_ENCODE = -2,
    FENNEC_ERR_DECODE = -3,
} fennec_status;

// Callback: delivers one H.264 NAL unit
typedef void (*fennec_nal_callback)(
    const uint8_t* nal_data, int nal_size,
    int64_t pts, int is_keyframe, void* user_data);

// Callback: delivers a decoded RGBA frame
typedef void (*fennec_frame_callback)(
    const uint8_t* rgba_data, int width, int height, void* user_data);

// --- Standalone encoder ---
FENNEC_API fennec_encoder* fennec_encoder_create(int width, int height, int bitrate_kbps, int fps);
FENNEC_API fennec_status   fennec_encoder_encode_rgba(fennec_encoder* enc, const uint8_t* rgba, int w, int h,
                    int64_t pts, int force_kf, fennec_nal_callback cb, void* ud);
FENNEC_API fennec_status   fennec_encoder_update_size(fennec_encoder* enc, int w, int h);
FENNEC_API fennec_status   fennec_encoder_update_bitrate(fennec_encoder* enc, int bitrate_kbps);
FENNEC_API fennec_status   fennec_encoder_update_fps(fennec_encoder* enc, int fps);
FENNEC_API const char*     fennec_encoder_get_name(fennec_encoder* enc);
FENNEC_API void            fennec_encoder_destroy(fennec_encoder* enc);

// --- Fused capture+encode ---
typedef struct {
    const char* id;
    const char* name;
    int width;
    int height;
    int is_window;
} fennec_capture_target;

FENNEC_API int             fennec_capture_list_targets(fennec_capture_target** out);
FENNEC_API void            fennec_capture_free_targets(fennec_capture_target* targets, int count);

FENNEC_API fennec_capture* fennec_capture_create(const char* target_id, int max_w, int max_h,
                    int bitrate_kbps, int fps,
                    fennec_nal_callback nal_cb,
                    fennec_frame_callback preview_cb,
                    void* user_data);
FENNEC_API fennec_status   fennec_capture_start(fennec_capture* cap);
FENNEC_API fennec_status   fennec_capture_stop(fennec_capture* cap);
FENNEC_API fennec_status   fennec_capture_update_bitrate(fennec_capture* cap, int bitrate_kbps);
FENNEC_API fennec_status   fennec_capture_update_fps(fennec_capture* cap, int fps);
FENNEC_API void            fennec_capture_destroy(fennec_capture* cap);

// --- Native picker (macOS 14+) ---
typedef void (*fennec_picker_selected_callback)(void* user_data);
typedef void (*fennec_picker_cancelled_callback)(void* user_data);

typedef struct fennec_picker fennec_picker;

FENNEC_API int             fennec_picker_is_available(void);
FENNEC_API fennec_picker*  fennec_picker_create(
                               int max_w, int max_h, int bitrate_kbps, int fps,
                               fennec_nal_callback nal_cb, fennec_frame_callback preview_cb,
                               fennec_picker_selected_callback on_selected,
                               fennec_picker_cancelled_callback on_cancelled,
                               void* user_data);
FENNEC_API fennec_status   fennec_picker_activate(fennec_picker* picker);
FENNEC_API fennec_status   fennec_picker_stop(fennec_picker* picker);
FENNEC_API fennec_status   fennec_picker_update_bitrate(fennec_picker* picker, int bitrate_kbps);
FENNEC_API fennec_status   fennec_picker_update_fps(fennec_picker* picker, int fps);
FENNEC_API void            fennec_picker_destroy(fennec_picker* picker);

// --- Decoder ---
FENNEC_API fennec_decoder* fennec_decoder_create(void);
FENNEC_API fennec_status   fennec_decoder_decode(fennec_decoder* dec, const uint8_t* nal, int size,
                    fennec_frame_callback cb, void* ud);
FENNEC_API void            fennec_decoder_destroy(fennec_decoder* dec);

#ifdef __cplusplus
}
#endif

#endif // FENNEC_VIDEO_H
