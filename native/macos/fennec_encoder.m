#import <VideoToolbox/VideoToolbox.h>
#import <CoreMedia/CoreMedia.h>
#import <CoreVideo/CoreVideo.h>
#include "fennec_video.h"
#include <stdlib.h>
#include <string.h>

struct fennec_encoder {
    VTCompressionSessionRef session;
    int width;
    int height;
    int bitrate_kbps;
    int fps;

    // Temporary storage for callback results during synchronous encode
    fennec_nal_callback nal_cb;
    void* user_data;
    int64_t current_pts;
};

static void encoderOutputCallback(void* outputCallbackRefCon,
    void* sourceFrameRefCon, OSStatus status, VTEncodeInfoFlags infoFlags,
    CMSampleBufferRef sampleBuffer) {

    if (status != noErr || !sampleBuffer) return;

    fennec_encoder* enc = (fennec_encoder*)outputCallbackRefCon;
    if (!enc->nal_cb) return;

    // Check if keyframe
    CFArrayRef attachments = CMSampleBufferGetSampleAttachmentsArray(sampleBuffer, false);
    int isKeyframe = 0;
    if (attachments && CFArrayGetCount(attachments) > 0) {
        CFDictionaryRef dict = CFArrayGetValueAtIndex(attachments, 0);
        CFBooleanRef notSync;
        if (CFDictionaryGetValueIfPresent(dict, kCMSampleAttachmentKey_NotSync, (const void**)&notSync)) {
            isKeyframe = !CFBooleanGetValue(notSync);
        } else {
            isKeyframe = 1;
        }
    }

    CMBlockBufferRef blockBuffer = CMSampleBufferGetDataBuffer(sampleBuffer);
    size_t totalLength = 0;
    char* dataPointer = NULL;
    CMBlockBufferGetDataPointer(blockBuffer, 0, NULL, &totalLength, &dataPointer);

    if (!dataPointer || totalLength == 0) return;

    CMTime pts = CMSampleBufferGetPresentationTimeStamp(sampleBuffer);
    int64_t ptsValue = (int64_t)(CMTimeGetSeconds(pts) * 90000);

    // Extract SPS/PPS on keyframes
    if (isKeyframe) {
        CMFormatDescriptionRef format = CMSampleBufferGetFormatDescription(sampleBuffer);
        if (format) {
            size_t spsSize = 0, ppsSize = 0;
            const uint8_t* sps = NULL;
            const uint8_t* pps = NULL;

            CMVideoFormatDescriptionGetH264ParameterSetAtIndex(format, 0, &sps, &spsSize, NULL, NULL);
            CMVideoFormatDescriptionGetH264ParameterSetAtIndex(format, 1, &pps, &ppsSize, NULL, NULL);

            if (sps && spsSize > 0)
                enc->nal_cb(sps, (int)spsSize, ptsValue, 1, enc->user_data);
            if (pps && ppsSize > 0)
                enc->nal_cb(pps, (int)ppsSize, ptsValue, 1, enc->user_data);
        }
    }

    // Parse AVCC NAL units
    size_t offset = 0;
    while (offset < totalLength) {
        uint32_t nalLength = 0;
        memcpy(&nalLength, dataPointer + offset, 4);
        nalLength = CFSwapInt32BigToHost(nalLength);
        offset += 4;

        if (nalLength > 0 && offset + nalLength <= totalLength) {
            enc->nal_cb((const uint8_t*)(dataPointer + offset), (int)nalLength,
                ptsValue, isKeyframe, enc->user_data);
        }
        offset += nalLength;
    }
}

static OSStatus createSession(fennec_encoder* enc) {
    OSStatus status = VTCompressionSessionCreate(NULL, enc->width, enc->height,
        kCMVideoCodecType_H264, NULL, NULL, NULL,
        encoderOutputCallback, enc, &enc->session);

    if (status != noErr) return status;

    VTSessionSetProperty(enc->session, kVTCompressionPropertyKey_RealTime, kCFBooleanTrue);
    VTSessionSetProperty(enc->session, kVTCompressionPropertyKey_ProfileLevel,
        kVTProfileLevel_H264_Main_AutoLevel);
    VTSessionSetProperty(enc->session, kVTCompressionPropertyKey_AllowFrameReordering, kCFBooleanFalse);

    int avgBitrate = enc->bitrate_kbps * 1000;
    CFNumberRef bitrateRef = CFNumberCreate(NULL, kCFNumberIntType, &avgBitrate);
    VTSessionSetProperty(enc->session, kVTCompressionPropertyKey_AverageBitRate, bitrateRef);
    CFRelease(bitrateRef);

    int maxKeyFrameInterval = enc->fps * 5;
    CFNumberRef kfRef = CFNumberCreate(NULL, kCFNumberIntType, &maxKeyFrameInterval);
    VTSessionSetProperty(enc->session, kVTCompressionPropertyKey_MaxKeyFrameInterval, kfRef);
    CFRelease(kfRef);

    VTCompressionSessionPrepareToEncodeFrames(enc->session);
    return noErr;
}

fennec_encoder* fennec_encoder_create(int width, int height, int bitrate_kbps, int fps) {
    fennec_encoder* enc = calloc(1, sizeof(fennec_encoder));
    if (!enc) return NULL;

    enc->width = width;
    enc->height = height;
    enc->bitrate_kbps = bitrate_kbps;
    enc->fps = fps;

    if (createSession(enc) != noErr) {
        free(enc);
        return NULL;
    }

    return enc;
}

fennec_status fennec_encoder_encode_rgba(fennec_encoder* enc, const uint8_t* rgba, int w, int h,
    int64_t pts, int force_kf, fennec_nal_callback cb, void* ud) {

    if (!enc || !enc->session) return FENNEC_ERR_ENCODE;

    // Store callback for use in the compression output callback
    enc->nal_cb = cb;
    enc->user_data = ud;
    enc->current_pts = pts;

    // Create CVPixelBuffer from RGBA data
    CVPixelBufferRef pixelBuffer = NULL;
    NSDictionary* attrs = @{
        (id)kCVPixelBufferWidthKey: @(w),
        (id)kCVPixelBufferHeightKey: @(h),
        (id)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
    };

    CVReturn cvStatus = CVPixelBufferCreate(NULL, w, h, kCVPixelFormatType_32BGRA,
        (__bridge CFDictionaryRef)attrs, &pixelBuffer);

    if (cvStatus != kCVReturnSuccess) return FENNEC_ERR_ENCODE;

    CVPixelBufferLockBaseAddress(pixelBuffer, 0);
    uint8_t* base = CVPixelBufferGetBaseAddress(pixelBuffer);
    size_t bytesPerRow = CVPixelBufferGetBytesPerRow(pixelBuffer);

    // Convert RGBA to BGRA
    for (int y = 0; y < h; y++) {
        const uint8_t* srcRow = rgba + y * w * 4;
        uint8_t* dstRow = base + y * bytesPerRow;
        for (int x = 0; x < w; x++) {
            dstRow[x*4+0] = srcRow[x*4+2]; // B
            dstRow[x*4+1] = srcRow[x*4+1]; // G
            dstRow[x*4+2] = srcRow[x*4+0]; // R
            dstRow[x*4+3] = srcRow[x*4+3]; // A
        }
    }

    CVPixelBufferUnlockBaseAddress(pixelBuffer, 0);

    CMTime cmPts = CMTimeMake(pts, 90000);
    CMTime duration = CMTimeMake(90000 / enc->fps, 90000);

    NSDictionary* frameProps = nil;
    if (force_kf) {
        frameProps = @{ (id)kVTEncodeFrameOptionKey_ForceKeyFrame: @YES };
    }

    VTEncodeInfoFlags infoFlags;
    OSStatus status = VTCompressionSessionEncodeFrame(enc->session, pixelBuffer,
        cmPts, duration, (__bridge CFDictionaryRef)frameProps, NULL, &infoFlags);

    CVPixelBufferRelease(pixelBuffer);

    // Flush to get synchronous output
    VTCompressionSessionCompleteFrames(enc->session, cmPts);

    enc->nal_cb = NULL;
    enc->user_data = NULL;

    return (status == noErr) ? FENNEC_OK : FENNEC_ERR_ENCODE;
}

fennec_status fennec_encoder_update_size(fennec_encoder* enc, int w, int h) {
    if (!enc) return FENNEC_ERR_INIT;

    if (enc->session) {
        VTCompressionSessionCompleteFrames(enc->session, kCMTimeInvalid);
        VTCompressionSessionInvalidate(enc->session);
        CFRelease(enc->session);
        enc->session = NULL;
    }

    enc->width = w;
    enc->height = h;

    if (createSession(enc) != noErr) return FENNEC_ERR_INIT;
    return FENNEC_OK;
}

fennec_status fennec_encoder_update_bitrate(fennec_encoder* enc, int bitrate_kbps) {
    if (!enc || !enc->session) return FENNEC_ERR_INIT;

    enc->bitrate_kbps = bitrate_kbps;

    int avgBitrate = bitrate_kbps * 1000;
    CFNumberRef bitrateRef = CFNumberCreate(NULL, kCFNumberIntType, &avgBitrate);
    VTSessionSetProperty(enc->session, kVTCompressionPropertyKey_AverageBitRate, bitrateRef);
    CFRelease(bitrateRef);

    return FENNEC_OK;
}

fennec_status fennec_encoder_update_fps(fennec_encoder* enc, int fps) {
    if (!enc) return FENNEC_ERR_INIT;

    enc->fps = fps;

    // FPS change requires session teardown/recreate (same as update_size)
    if (enc->session) {
        VTCompressionSessionCompleteFrames(enc->session, kCMTimeInvalid);
        VTCompressionSessionInvalidate(enc->session);
        CFRelease(enc->session);
        enc->session = NULL;
    }

    if (createSession(enc) != noErr) return FENNEC_ERR_INIT;
    return FENNEC_OK;
}

void fennec_encoder_destroy(fennec_encoder* enc) {
    if (!enc) return;

    if (enc->session) {
        VTCompressionSessionCompleteFrames(enc->session, kCMTimeInvalid);
        VTCompressionSessionInvalidate(enc->session);
        CFRelease(enc->session);
    }

    free(enc);
}
