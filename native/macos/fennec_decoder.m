#import <VideoToolbox/VideoToolbox.h>
#import <CoreMedia/CoreMedia.h>
#import <CoreVideo/CoreVideo.h>
#include "fennec_video.h"
#include <stdlib.h>
#include <string.h>

struct fennec_decoder {
    VTDecompressionSessionRef session;
    CMFormatDescriptionRef formatDesc;

    // Saved SPS/PPS for format description creation
    uint8_t* sps;
    int sps_size;
    uint8_t* pps;
    int pps_size;

    // Temporary storage for synchronous decode callback
    fennec_frame_callback frame_cb;
    void* user_data;
};

static void decoderOutputCallback(void* decompressionOutputRefCon,
    void* sourceFrameRefCon, OSStatus status, VTDecodeInfoFlags infoFlags,
    CVImageBufferRef imageBuffer, CMTime presentationTimeStamp, CMTime presentationDuration) {

    if (status != noErr || !imageBuffer) return;

    fennec_decoder* dec = (fennec_decoder*)decompressionOutputRefCon;
    if (!dec->frame_cb) return;

    CVPixelBufferLockBaseAddress(imageBuffer, kCVPixelBufferLock_ReadOnly);

    int width = (int)CVPixelBufferGetWidth(imageBuffer);
    int height = (int)CVPixelBufferGetHeight(imageBuffer);
    size_t bytesPerRow = CVPixelBufferGetBytesPerRow(imageBuffer);
    uint8_t* baseAddress = CVPixelBufferGetBaseAddress(imageBuffer);

    if (baseAddress) {
        int rgbaSize = width * height * 4;
        uint8_t* rgba = malloc(rgbaSize);
        if (rgba) {
            // Convert BGRA to RGBA
            for (int y = 0; y < height; y++) {
                uint8_t* srcRow = baseAddress + y * bytesPerRow;
                uint8_t* dstRow = rgba + y * width * 4;
                for (int x = 0; x < width; x++) {
                    dstRow[x*4+0] = srcRow[x*4+2]; // R
                    dstRow[x*4+1] = srcRow[x*4+1]; // G
                    dstRow[x*4+2] = srcRow[x*4+0]; // B
                    dstRow[x*4+3] = 255;            // A
                }
            }
            dec->frame_cb(rgba, width, height, dec->user_data);
            free(rgba);
        }
    }

    CVPixelBufferUnlockBaseAddress(imageBuffer, kCVPixelBufferLock_ReadOnly);
}

static OSStatus createDecoderSession(fennec_decoder* dec) {
    if (!dec->sps || !dec->pps) return -1;

    // Destroy old session if exists
    if (dec->session) {
        VTDecompressionSessionInvalidate(dec->session);
        CFRelease(dec->session);
        dec->session = NULL;
    }
    if (dec->formatDesc) {
        CFRelease(dec->formatDesc);
        dec->formatDesc = NULL;
    }

    // Create format description from SPS/PPS
    const uint8_t* paramSets[] = { dec->sps, dec->pps };
    const size_t paramSizes[] = { (size_t)dec->sps_size, (size_t)dec->pps_size };

    OSStatus status = CMVideoFormatDescriptionCreateFromH264ParameterSets(NULL,
        2, paramSets, paramSizes, 4, &dec->formatDesc);

    if (status != noErr) return status;

    // Output pixel format: BGRA
    NSDictionary* destAttrs = @{
        (id)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
    };

    VTDecompressionOutputCallbackRecord callbackRecord = {
        .decompressionOutputCallback = decoderOutputCallback,
        .decompressionOutputRefCon = dec,
    };

    status = VTDecompressionSessionCreate(NULL, dec->formatDesc, NULL,
        (__bridge CFDictionaryRef)destAttrs, &callbackRecord, &dec->session);

    return status;
}

fennec_decoder* fennec_decoder_create(void) {
    fennec_decoder* dec = calloc(1, sizeof(fennec_decoder));
    return dec;
}

fennec_status fennec_decoder_decode(fennec_decoder* dec, const uint8_t* nal, int size,
    fennec_frame_callback cb, void* ud) {

    if (!dec || !nal || size < 1) return FENNEC_ERR_DECODE;

    uint8_t nalType = nal[0] & 0x1F;

    // SPS (type 7)
    if (nalType == 7) {
        free(dec->sps);
        dec->sps = malloc(size);
        memcpy(dec->sps, nal, size);
        dec->sps_size = size;
        return FENNEC_OK;
    }

    // PPS (type 8)
    if (nalType == 8) {
        free(dec->pps);
        dec->pps = malloc(size);
        memcpy(dec->pps, nal, size);
        dec->pps_size = size;

        // Recreate session with new parameters
        if (dec->sps) {
            OSStatus status = createDecoderSession(dec);
            if (status != noErr) return FENNEC_ERR_INIT;
        }
        return FENNEC_OK;
    }

    // IDR (type 5) or non-IDR (type 1) — actual video data
    if (!dec->session) return FENNEC_ERR_DECODE;

    // Wrap NAL in AVCC format: [4-byte big-endian length][NAL data]
    size_t avccSize = 4 + size;
    uint8_t* avccData = malloc(avccSize);
    if (!avccData) return FENNEC_ERR_DECODE;

    uint32_t nalLength = CFSwapInt32HostToBig((uint32_t)size);
    memcpy(avccData, &nalLength, 4);
    memcpy(avccData + 4, nal, size);

    // Create CMBlockBuffer
    CMBlockBufferRef blockBuffer = NULL;
    OSStatus status = CMBlockBufferCreateWithMemoryBlock(NULL, avccData, avccSize,
        kCFAllocatorDefault, NULL, 0, avccSize, 0, &blockBuffer);

    if (status != noErr) {
        free(avccData);
        return FENNEC_ERR_DECODE;
    }

    // Create CMSampleBuffer
    CMSampleBufferRef sampleBuffer = NULL;
    const size_t sampleSizes[] = { avccSize };

    status = CMSampleBufferCreateReady(NULL, blockBuffer, dec->formatDesc,
        1, 0, NULL, 1, sampleSizes, &sampleBuffer);

    if (status != noErr) {
        CFRelease(blockBuffer);
        return FENNEC_ERR_DECODE;
    }

    // Set callback for this decode
    dec->frame_cb = cb;
    dec->user_data = ud;

    // Decode
    VTDecodeInfoFlags infoFlags;
    status = VTDecompressionSessionDecodeFrame(dec->session, sampleBuffer,
        kVTDecodeFrame_1xRealTimePlayback, NULL, &infoFlags);

    // Wait for output
    if (status == noErr) {
        VTDecompressionSessionWaitForAsynchronousFrames(dec->session);
    }

    dec->frame_cb = NULL;
    dec->user_data = NULL;

    CFRelease(sampleBuffer);
    CFRelease(blockBuffer);

    return (status == noErr) ? FENNEC_OK : FENNEC_ERR_DECODE;
}

void fennec_decoder_destroy(fennec_decoder* dec) {
    if (!dec) return;

    if (dec->session) {
        VTDecompressionSessionInvalidate(dec->session);
        CFRelease(dec->session);
    }
    if (dec->formatDesc) {
        CFRelease(dec->formatDesc);
    }

    free(dec->sps);
    free(dec->pps);
    free(dec);
}
