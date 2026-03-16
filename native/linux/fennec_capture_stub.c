#include "fennec_video.h"
#include <stdlib.h>

// Linux uses existing PipeWire/X11 capture services from C#.
// These stubs satisfy the linker for the shared capture API.

int fennec_capture_list_targets(fennec_capture_target** out) {
    *out = NULL;
    return 0;
}

void fennec_capture_free_targets(fennec_capture_target* targets, int count) {
    (void)targets;
    (void)count;
}

fennec_capture* fennec_capture_create(const char* target_id, int max_w, int max_h,
    int bitrate_kbps, int fps,
    fennec_nal_callback nal_cb, fennec_frame_callback preview_cb, void* user_data) {
    (void)target_id; (void)max_w; (void)max_h;
    (void)bitrate_kbps; (void)fps;
    (void)nal_cb; (void)preview_cb; (void)user_data;
    return NULL;
}

fennec_status fennec_capture_start(fennec_capture* cap) {
    (void)cap;
    return FENNEC_ERR_INIT;
}

fennec_status fennec_capture_stop(fennec_capture* cap) {
    (void)cap;
    return FENNEC_ERR_INIT;
}

fennec_status fennec_capture_update_bitrate(fennec_capture* cap, int bitrate_kbps) {
    (void)cap; (void)bitrate_kbps;
    return FENNEC_ERR_INIT;
}

fennec_status fennec_capture_update_fps(fennec_capture* cap, int fps) {
    (void)cap; (void)fps;
    return FENNEC_ERR_INIT;
}

void fennec_capture_destroy(fennec_capture* cap) {
    (void)cap;
}
