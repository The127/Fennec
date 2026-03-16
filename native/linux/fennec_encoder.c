#include "fennec_video.h"
#include <libavcodec/avcodec.h>
#include <libavutil/hwcontext.h>
#include <libavutil/imgutils.h>
#include <libavutil/opt.h>
#include <libswscale/swscale.h>
#include <stdlib.h>
#include <string.h>

struct fennec_encoder {
    const AVCodec* codec;
    AVCodecContext* ctx;
    AVFrame* frame;
    AVPacket* pkt;
    struct SwsContext* sws;
    int width;
    int height;
    int bitrate_kbps;
    int fps;
    enum AVPixelFormat target_pix_fmt;
    AVBufferRef* hw_device_ctx;
    AVBufferRef* hw_frames_ctx;
};

// Try codecs in priority order: NVIDIA HW -> VA-API HW -> SW
static const char* encoder_names[] = {
    "h264_nvenc",
    "h264_vaapi",
    "libx264",
    "libopenh264",
    NULL
};

static int init_vaapi_ctx(fennec_encoder* enc) {
    int ret = av_hwdevice_ctx_create(&enc->hw_device_ctx,
        AV_HWDEVICE_TYPE_VAAPI, NULL, NULL, 0);
    if (ret < 0) return ret;

    enc->hw_frames_ctx = av_hwframe_ctx_alloc(enc->hw_device_ctx);
    if (!enc->hw_frames_ctx) return AVERROR(ENOMEM);

    AVHWFramesContext* fctx = (AVHWFramesContext*)enc->hw_frames_ctx->data;
    fctx->format    = AV_PIX_FMT_VAAPI;
    fctx->sw_format = AV_PIX_FMT_NV12;
    fctx->width     = enc->width;
    fctx->height    = enc->height;
    fctx->initial_pool_size = 20;

    ret = av_hwframe_ctx_init(enc->hw_frames_ctx);
    if (ret < 0) {
        av_buffer_unref(&enc->hw_frames_ctx);
        av_buffer_unref(&enc->hw_device_ctx);
        return ret;
    }

    enc->ctx->hw_frames_ctx = av_buffer_ref(enc->hw_frames_ctx);
    return 0;
}

static void init_frame_and_sws(fennec_encoder* enc) {
    enc->frame = av_frame_alloc();
    enc->frame->format = enc->target_pix_fmt;
    enc->frame->width = enc->width;
    enc->frame->height = enc->height;
    av_frame_get_buffer(enc->frame, 0);

    enc->pkt = av_packet_alloc();

    enc->sws = sws_getContext(enc->width, enc->height, AV_PIX_FMT_RGBA,
        enc->width, enc->height, enc->target_pix_fmt,
        SWS_FAST_BILINEAR, NULL, NULL, NULL);
}

static int init_encoder_ctx(fennec_encoder* enc) {
    enc->ctx = avcodec_alloc_context3(enc->codec);
    if (!enc->ctx) return -1;

    enc->ctx->width = enc->width;
    enc->ctx->height = enc->height;
    enc->ctx->time_base = (AVRational){1, 90000}; // RTP timebase
    enc->ctx->framerate = (AVRational){enc->fps, 1};
    enc->ctx->bit_rate = enc->bitrate_kbps * 1000;
    enc->ctx->gop_size = enc->fps * 5; // keyframe every 5 seconds
    enc->ctx->max_b_frames = 0; // no B-frames for low latency
    enc->ctx->thread_count = 2;

    // Per-encoder pixel format and tuning
    if (strcmp(enc->codec->name, "h264_nvenc") == 0) {
        enc->ctx->pix_fmt = AV_PIX_FMT_NV12;
        enc->target_pix_fmt = AV_PIX_FMT_NV12;
        av_opt_set(enc->ctx->priv_data, "preset", "p1", 0);
        av_opt_set(enc->ctx->priv_data, "tune", "ull", 0);
        av_opt_set(enc->ctx->priv_data, "rc", "cbr", 0);
        av_opt_set(enc->ctx->priv_data, "zerolatency", "1", 0);
    } else if (strcmp(enc->codec->name, "h264_vaapi") == 0) {
        enc->ctx->pix_fmt = AV_PIX_FMT_VAAPI;
        enc->target_pix_fmt = AV_PIX_FMT_NV12;
        if (init_vaapi_ctx(enc) < 0) {
            avcodec_free_context(&enc->ctx);
            return -1;
        }
    } else if (strcmp(enc->codec->name, "libx264") == 0) {
        enc->ctx->pix_fmt = AV_PIX_FMT_YUV420P;
        enc->target_pix_fmt = AV_PIX_FMT_YUV420P;
        av_opt_set(enc->ctx->priv_data, "preset", "ultrafast", 0);
        av_opt_set(enc->ctx->priv_data, "tune", "zerolatency", 0);
    } else {
        enc->ctx->pix_fmt = AV_PIX_FMT_YUV420P;
        enc->target_pix_fmt = AV_PIX_FMT_YUV420P;
        av_opt_set(enc->ctx->priv_data, "allow_skip_frames", "1", 0);
    }

    int ret = avcodec_open2(enc->ctx, enc->codec, NULL);
    if (ret < 0) {
        if (enc->hw_frames_ctx) { av_buffer_unref(&enc->hw_frames_ctx); enc->hw_frames_ctx = NULL; }
        if (enc->hw_device_ctx) { av_buffer_unref(&enc->hw_device_ctx); enc->hw_device_ctx = NULL; }
        avcodec_free_context(&enc->ctx);
        return ret;
    }

    return 0;
}

fennec_encoder* fennec_encoder_create(int width, int height, int bitrate_kbps, int fps) {
    fennec_encoder* enc = calloc(1, sizeof(fennec_encoder));
    if (!enc) return NULL;

    enc->width = width & ~1;  // ensure even
    enc->height = height & ~1;
    enc->bitrate_kbps = bitrate_kbps;
    enc->fps = fps;

    // Try encoders in priority order
    for (int i = 0; encoder_names[i]; i++) {
        enc->codec = avcodec_find_encoder_by_name(encoder_names[i]);
        if (enc->codec && init_encoder_ctx(enc) == 0) {
            break;
        }
        enc->codec = NULL;
        enc->ctx = NULL;
    }

    if (!enc->ctx) {
        free(enc);
        return NULL;
    }

    init_frame_and_sws(enc);

    return enc;
}

fennec_status fennec_encoder_encode_rgba(fennec_encoder* enc, const uint8_t* rgba, int w, int h,
    int64_t pts, int force_kf, fennec_nal_callback cb, void* ud) {

    if (!enc || !enc->ctx) return FENNEC_ERR_ENCODE;

    // If input size differs from encoder, use swscale to resize+convert
    struct SwsContext* sws = enc->sws;
    if (w != enc->width || h != enc->height) {
        sws = sws_getContext(w, h, AV_PIX_FMT_RGBA,
            enc->width, enc->height, enc->target_pix_fmt,
            SWS_FAST_BILINEAR, NULL, NULL, NULL);
        if (!sws) return FENNEC_ERR_ENCODE;
    }

    const uint8_t* srcSlice[] = { rgba };
    int srcStride[] = { w * 4 };

    av_frame_make_writable(enc->frame);
    sws_scale(sws, srcSlice, srcStride, 0, h,
        enc->frame->data, enc->frame->linesize);

    if (sws != enc->sws) sws_freeContext(sws);

    enc->frame->pts = pts;
    if (force_kf) {
        enc->frame->pict_type = AV_PICTURE_TYPE_I;
        enc->frame->flags |= AV_FRAME_FLAG_KEY;
    } else {
        enc->frame->pict_type = AV_PICTURE_TYPE_NONE;
        enc->frame->flags &= ~AV_FRAME_FLAG_KEY;
    }

    int ret;
    if (enc->hw_frames_ctx) {
        AVFrame* hw_frame = av_frame_alloc();
        av_hwframe_get_buffer(enc->ctx->hw_frames_ctx, hw_frame, 0);
        av_hwframe_transfer_data(hw_frame, enc->frame, 0);
        hw_frame->pts = enc->frame->pts;
        hw_frame->pict_type = enc->frame->pict_type;
        hw_frame->flags = enc->frame->flags;
        ret = avcodec_send_frame(enc->ctx, hw_frame);
        av_frame_free(&hw_frame);
    } else {
        ret = avcodec_send_frame(enc->ctx, enc->frame);
    }
    if (ret < 0) return FENNEC_ERR_ENCODE;

    while (ret >= 0) {
        ret = avcodec_receive_packet(enc->ctx, enc->pkt);
        if (ret < 0) break;

        int is_keyframe = (enc->pkt->flags & AV_PKT_FLAG_KEY) != 0;

        // Parse Annex B start codes to extract individual NAL units
        uint8_t* data = enc->pkt->data;
        int size = enc->pkt->size;
        int offset = 0;

        while (offset < size) {
            // Find start code (0x00 0x00 0x01 or 0x00 0x00 0x00 0x01)
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

        av_packet_unref(enc->pkt);
    }

    return FENNEC_OK;
}

fennec_status fennec_encoder_update_size(fennec_encoder* enc, int w, int h) {
    if (!enc) return FENNEC_ERR_INIT;

    w &= ~1;
    h &= ~1;

    if (w == enc->width && h == enc->height) return FENNEC_OK;

    // Tear down old encoder
    if (enc->sws) { sws_freeContext(enc->sws); enc->sws = NULL; }
    if (enc->pkt) { av_packet_free(&enc->pkt); }
    if (enc->frame) { av_frame_free(&enc->frame); }
    if (enc->hw_frames_ctx) { av_buffer_unref(&enc->hw_frames_ctx); enc->hw_frames_ctx = NULL; }
    if (enc->hw_device_ctx) { av_buffer_unref(&enc->hw_device_ctx); enc->hw_device_ctx = NULL; }
    if (enc->ctx) { avcodec_free_context(&enc->ctx); }

    enc->width = w;
    enc->height = h;

    if (init_encoder_ctx(enc) != 0) return FENNEC_ERR_INIT;

    init_frame_and_sws(enc);

    return FENNEC_OK;
}

fennec_status fennec_encoder_update_bitrate(fennec_encoder* enc, int bitrate_kbps) {
    if (!enc) return FENNEC_ERR_INIT;

    enc->bitrate_kbps = bitrate_kbps;

    // Tear down old encoder context
    if (enc->sws) { sws_freeContext(enc->sws); enc->sws = NULL; }
    if (enc->pkt) { av_packet_free(&enc->pkt); }
    if (enc->frame) { av_frame_free(&enc->frame); }
    if (enc->hw_frames_ctx) { av_buffer_unref(&enc->hw_frames_ctx); enc->hw_frames_ctx = NULL; }
    if (enc->hw_device_ctx) { av_buffer_unref(&enc->hw_device_ctx); enc->hw_device_ctx = NULL; }
    if (enc->ctx) { avcodec_free_context(&enc->ctx); }

    if (init_encoder_ctx(enc) != 0) return FENNEC_ERR_INIT;

    init_frame_and_sws(enc);

    return FENNEC_OK;
}

fennec_status fennec_encoder_update_fps(fennec_encoder* enc, int fps) {
    if (!enc) return FENNEC_ERR_INIT;

    enc->fps = fps;

    // Tear down old encoder context
    if (enc->sws) { sws_freeContext(enc->sws); enc->sws = NULL; }
    if (enc->pkt) { av_packet_free(&enc->pkt); }
    if (enc->frame) { av_frame_free(&enc->frame); }
    if (enc->hw_frames_ctx) { av_buffer_unref(&enc->hw_frames_ctx); enc->hw_frames_ctx = NULL; }
    if (enc->hw_device_ctx) { av_buffer_unref(&enc->hw_device_ctx); enc->hw_device_ctx = NULL; }
    if (enc->ctx) { avcodec_free_context(&enc->ctx); }

    if (init_encoder_ctx(enc) != 0) return FENNEC_ERR_INIT;

    init_frame_and_sws(enc);

    return FENNEC_OK;
}

const char* fennec_encoder_get_name(fennec_encoder* enc) {
    if (!enc || !enc->codec) return "unknown";
    return enc->codec->name;
}

void fennec_encoder_destroy(fennec_encoder* enc) {
    if (!enc) return;
    if (enc->sws) sws_freeContext(enc->sws);
    if (enc->pkt) av_packet_free(&enc->pkt);
    if (enc->frame) av_frame_free(&enc->frame);
    if (enc->hw_frames_ctx) av_buffer_unref(&enc->hw_frames_ctx);
    if (enc->hw_device_ctx) av_buffer_unref(&enc->hw_device_ctx);
    if (enc->ctx) avcodec_free_context(&enc->ctx);
    free(enc);
}
