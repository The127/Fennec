#include "fennec_video.h"
#include <libavcodec/avcodec.h>
#include <libavutil/imgutils.h>
#include <libswscale/swscale.h>
#include <stdlib.h>
#include <string.h>

struct fennec_decoder {
    const AVCodec* codec;
    AVCodecContext* ctx;
    AVFrame* frame;
    AVPacket* pkt;
    struct SwsContext* sws;
    int last_width;
    int last_height;
};

// Try decoders in priority order: h264_vaapi (HW) -> h264 (SW)
static const char* decoder_names[] = {
    "h264",  // ffmpeg's built-in software H.264 decoder (most reliable)
    NULL
};

fennec_decoder* fennec_decoder_create(void) {
    fennec_decoder* dec = calloc(1, sizeof(fennec_decoder));
    if (!dec) return NULL;

    for (int i = 0; decoder_names[i]; i++) {
        dec->codec = avcodec_find_decoder_by_name(decoder_names[i]);
        if (dec->codec) break;
    }

    // Fallback: find by codec ID
    if (!dec->codec) {
        dec->codec = avcodec_find_decoder(AV_CODEC_ID_H264);
    }

    if (!dec->codec) {
        free(dec);
        return NULL;
    }

    dec->ctx = avcodec_alloc_context3(dec->codec);
    if (!dec->ctx) {
        free(dec);
        return NULL;
    }

    dec->ctx->thread_count = 2;

    if (avcodec_open2(dec->ctx, dec->codec, NULL) < 0) {
        avcodec_free_context(&dec->ctx);
        free(dec);
        return NULL;
    }

    dec->frame = av_frame_alloc();
    dec->pkt = av_packet_alloc();

    return dec;
}

fennec_status fennec_decoder_decode(fennec_decoder* dec, const uint8_t* nal, int size,
    fennec_frame_callback cb, void* ud) {

    if (!dec || !dec->ctx || !nal || size < 1) return FENNEC_ERR_DECODE;

    // Wrap NAL in Annex B format: [start code][NAL data]
    int annex_b_size = 4 + size;
    uint8_t* annex_b = malloc(annex_b_size);
    if (!annex_b) return FENNEC_ERR_DECODE;

    annex_b[0] = 0x00;
    annex_b[1] = 0x00;
    annex_b[2] = 0x00;
    annex_b[3] = 0x01;
    memcpy(annex_b + 4, nal, size);

    dec->pkt->data = annex_b;
    dec->pkt->size = annex_b_size;

    int ret = avcodec_send_packet(dec->ctx, dec->pkt);
    free(annex_b);

    if (ret < 0) return FENNEC_ERR_DECODE;

    while (ret >= 0) {
        ret = avcodec_receive_frame(dec->ctx, dec->frame);
        if (ret < 0) break;

        int width = dec->frame->width;
        int height = dec->frame->height;

        // Recreate swscale context if resolution changed
        if (width != dec->last_width || height != dec->last_height) {
            if (dec->sws) sws_freeContext(dec->sws);
            dec->sws = sws_getContext(width, height, dec->frame->format,
                width, height, AV_PIX_FMT_RGBA,
                SWS_FAST_BILINEAR, NULL, NULL, NULL);
            dec->last_width = width;
            dec->last_height = height;
        }

        if (!dec->sws) continue;

        // Convert to RGBA
        int rgba_size = width * height * 4;
        uint8_t* rgba = malloc(rgba_size);
        if (!rgba) continue;

        uint8_t* dst_data[] = { rgba };
        int dst_linesize[] = { width * 4 };

        sws_scale(dec->sws, (const uint8_t* const*)dec->frame->data,
            dec->frame->linesize, 0, height, dst_data, dst_linesize);

        cb(rgba, width, height, ud);
        free(rgba);
    }

    return FENNEC_OK;
}

void fennec_decoder_destroy(fennec_decoder* dec) {
    if (!dec) return;
    if (dec->sws) sws_freeContext(dec->sws);
    if (dec->pkt) av_packet_free(&dec->pkt);
    if (dec->frame) av_frame_free(&dec->frame);
    if (dec->ctx) avcodec_free_context(&dec->ctx);
    free(dec);
}
