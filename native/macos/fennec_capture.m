#import <ScreenCaptureKit/ScreenCaptureKit.h>
#import <VideoToolbox/VideoToolbox.h>
#import <CoreMedia/CoreMedia.h>
#import <CoreVideo/CoreVideo.h>
#import <CoreGraphics/CoreGraphics.h>
#import <AppKit/AppKit.h>
#include "fennec_video.h"
#include "fennec_capture_internal.h"
#include <stdlib.h>
#include <string.h>

// --- Capture target listing ---

int fennec_capture_list_targets(fennec_capture_target** out) {
    __block fennec_capture_target* targets = NULL;
    __block int count = 0;

    dispatch_semaphore_t sem = dispatch_semaphore_create(0);

    [SCShareableContent getShareableContentWithCompletionHandler:^(SCShareableContent* content, NSError* error) {
        if (error || !content) {
            dispatch_semaphore_signal(sem);
            return;
        }

        NSArray<SCDisplay*>* displays = content.displays;
        NSArray<SCWindow*>* windows = content.windows;

        // Filter windows: must have title, be on-screen, and not be too small
        NSMutableArray<SCWindow*>* validWindows = [NSMutableArray array];
        for (SCWindow* w in windows) {
            if (w.title.length > 0 && w.isOnScreen && w.frame.size.width > 100 && w.frame.size.height > 100) {
                [validWindows addObject:w];
            }
        }

        count = (int)displays.count + (int)validWindows.count;
        targets = calloc(count, sizeof(fennec_capture_target));

        int idx = 0;
        for (SCDisplay* d in displays) {
            NSString* idStr = [NSString stringWithFormat:@"display:%u", d.displayID];
            NSString* name = [NSString stringWithFormat:@"Display %u (%dx%d)",
                d.displayID, (int)d.width, (int)d.height];
            targets[idx].id = strdup(idStr.UTF8String);
            targets[idx].name = strdup(name.UTF8String);
            targets[idx].width = (int)d.width;
            targets[idx].height = (int)d.height;
            targets[idx].is_window = 0;
            idx++;
        }

        for (SCWindow* w in validWindows) {
            NSString* idStr = [NSString stringWithFormat:@"window:%u", w.windowID];
            NSString* name = w.title ? [NSString stringWithFormat:@"%@ - %@",
                w.owningApplication.applicationName ?: @"Unknown", w.title] : @"Untitled";
            targets[idx].id = strdup(idStr.UTF8String);
            targets[idx].name = strdup(name.UTF8String);
            targets[idx].width = (int)w.frame.size.width;
            targets[idx].height = (int)w.frame.size.height;
            targets[idx].is_window = 1;
            idx++;
        }

        count = idx;
        dispatch_semaphore_signal(sem);
    }];

    dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, 5 * NSEC_PER_SEC));

    *out = targets;
    return count;
}

void fennec_capture_free_targets(fennec_capture_target* targets, int count) {
    if (!targets) return;
    for (int i = 0; i < count; i++) {
        free((void*)targets[i].id);
        free((void*)targets[i].name);
    }
    free(targets);
}

// --- Fused capture + encode ---

// --- Shared VT + delegate helpers (used by both capture and picker) ---

struct fennec_capture {
    SCStream* stream;
    SCContentFilter* filter;
    FennecCaptureDelegate* delegate;
    dispatch_queue_t queue;
    VTCompressionSessionRef session;
    int max_w;
    int max_h;
    int bitrate_kbps;
    int fps;
    char* target_id;
};

void fennec_vt_compression_output_callback(void* outputCallbackRefCon,
    void* sourceFrameRefCon, OSStatus status, VTEncodeInfoFlags infoFlags,
    CMSampleBufferRef sampleBuffer) {

    if (status != noErr || !sampleBuffer) return;

    FennecCaptureDelegate* del = (__bridge FennecCaptureDelegate*)outputCallbackRefCon;

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

    // Parse AVCC format: [4-byte length][NAL unit]...
    size_t offset = 0;
    CMTime pts = CMSampleBufferGetPresentationTimeStamp(sampleBuffer);
    int64_t ptsValue = (int64_t)(CMTimeGetSeconds(pts) * 90000); // RTP timebase

    // If keyframe, first extract SPS/PPS from format description
    if (isKeyframe) {
        CMFormatDescriptionRef format = CMSampleBufferGetFormatDescription(sampleBuffer);
        if (format) {
            size_t spsSize = 0, ppsSize = 0;
            const uint8_t* sps = NULL;
            const uint8_t* pps = NULL;
            size_t paramCount = 0;

            CMVideoFormatDescriptionGetH264ParameterSetAtIndex(format, 0, &sps, &spsSize, &paramCount, NULL);
            CMVideoFormatDescriptionGetH264ParameterSetAtIndex(format, 1, &pps, &ppsSize, NULL, NULL);

            if (sps && spsSize > 0) {
                del.nalCallback(sps, (int)spsSize, ptsValue, 1, del.userData);
            }
            if (pps && ppsSize > 0) {
                del.nalCallback(pps, (int)ppsSize, ptsValue, 1, del.userData);
            }
        }
    }

    while (offset < totalLength) {
        uint32_t nalLength = 0;
        memcpy(&nalLength, dataPointer + offset, 4);
        nalLength = CFSwapInt32BigToHost(nalLength);
        offset += 4;

        if (nalLength > 0 && offset + nalLength <= totalLength) {
            del.nalCallback((const uint8_t*)(dataPointer + offset), (int)nalLength,
                ptsValue, isKeyframe, del.userData);
        }
        offset += nalLength;
    }
}

OSStatus fennec_vt_create_session(int width, int height, int bitrate_kbps, int fps,
    FennecCaptureDelegate* delegate, VTCompressionSessionRef* outSession) {

    OSStatus vtStatus = VTCompressionSessionCreate(NULL, width, height,
        kCMVideoCodecType_H264, NULL, NULL, NULL,
        fennec_vt_compression_output_callback, (__bridge void*)delegate, outSession);

    if (vtStatus != noErr) return vtStatus;

    VTCompressionSessionRef session = *outSession;

    VTSessionSetProperty(session, kVTCompressionPropertyKey_RealTime, kCFBooleanTrue);
    VTSessionSetProperty(session, kVTCompressionPropertyKey_ProfileLevel,
        kVTProfileLevel_H264_Main_AutoLevel);
    VTSessionSetProperty(session, kVTCompressionPropertyKey_AllowFrameReordering, kCFBooleanFalse);

    int avgBitrate = bitrate_kbps * 1000;
    CFNumberRef bitrateRef = CFNumberCreate(NULL, kCFNumberIntType, &avgBitrate);
    VTSessionSetProperty(session, kVTCompressionPropertyKey_AverageBitRate, bitrateRef);
    CFRelease(bitrateRef);

    int bytesPerSec = avgBitrate * 3 / 2 / 8;
    double oneSecond = 1.0;
    CFNumberRef bytesRef = CFNumberCreate(NULL, kCFNumberIntType, &bytesPerSec);
    CFNumberRef durationRef = CFNumberCreate(NULL, kCFNumberDoubleType, &oneSecond);
    const void* limitValues[] = { bytesRef, durationRef };
    CFArrayRef dataRateLimit = CFArrayCreate(NULL, limitValues, 2, &kCFTypeArrayCallBacks);
    VTSessionSetProperty(session, kVTCompressionPropertyKey_DataRateLimits, dataRateLimit);
    CFRelease(bytesRef);
    CFRelease(durationRef);
    CFRelease(dataRateLimit);

    int maxKeyFrameInterval = fps * 5;
    CFNumberRef kfRef = CFNumberCreate(NULL, kCFNumberIntType, &maxKeyFrameInterval);
    VTSessionSetProperty(session, kVTCompressionPropertyKey_MaxKeyFrameInterval, kfRef);
    CFRelease(kfRef);

    VTCompressionSessionPrepareToEncodeFrames(session);

    return noErr;
}

fennec_status fennec_vt_update_bitrate(VTCompressionSessionRef session, int bitrate_kbps) {
    if (!session) return FENNEC_ERR_INIT;

    int avgBitrate = bitrate_kbps * 1000;
    CFNumberRef bitrateRef = CFNumberCreate(NULL, kCFNumberIntType, &avgBitrate);
    VTSessionSetProperty(session, kVTCompressionPropertyKey_AverageBitRate, bitrateRef);
    CFRelease(bitrateRef);

    int bytesPerSec = avgBitrate * 3 / 2 / 8;
    double oneSecond = 1.0;
    CFNumberRef bytesRef = CFNumberCreate(NULL, kCFNumberIntType, &bytesPerSec);
    CFNumberRef durationRef = CFNumberCreate(NULL, kCFNumberDoubleType, &oneSecond);
    const void* limitValues[] = { bytesRef, durationRef };
    CFArrayRef dataRateLimit = CFArrayCreate(NULL, limitValues, 2, &kCFTypeArrayCallBacks);
    VTSessionSetProperty(session, kVTCompressionPropertyKey_DataRateLimits, dataRateLimit);
    CFRelease(bytesRef);
    CFRelease(durationRef);
    CFRelease(dataRateLimit);

    return FENNEC_OK;
}

@implementation FennecCaptureDelegate

- (void)stream:(SCStream*)stream didOutputSampleBuffer:(CMSampleBufferRef)sampleBuffer ofType:(SCStreamOutputType)type {
    if (type != SCStreamOutputTypeScreen) return;

    CVImageBufferRef imageBuffer = CMSampleBufferGetImageBuffer(sampleBuffer);
    if (!imageBuffer) return;

    // Encode via VideoToolbox (zero-copy: CVPixelBuffer backed by IOSurface)
    CMTime pts = CMTimeMake(self.frameCount, self.fps);
    CMTime duration = CMTimeMake(1, self.fps);

    VTEncodeInfoFlags flags;
    OSStatus status = VTCompressionSessionEncodeFrame(self.compressionSession,
        imageBuffer, pts, duration, NULL, NULL, &flags);

    if (status != noErr) {
        // Silently drop frame on encode failure
    }

    // Deliver low-res preview every N frames
    if (self.previewCallback && (self.frameCount % self.previewInterval == 0)) {
        CVPixelBufferLockBaseAddress(imageBuffer, kCVPixelBufferLock_ReadOnly);

        int width = (int)CVPixelBufferGetWidth(imageBuffer);
        int height = (int)CVPixelBufferGetHeight(imageBuffer);
        size_t bytesPerRow = CVPixelBufferGetBytesPerRow(imageBuffer);
        uint8_t* baseAddress = CVPixelBufferGetBaseAddress(imageBuffer);

        if (baseAddress) {
            // Convert BGRA to RGBA in-place copy
            int rgbaSize = width * height * 4;
            uint8_t* rgba = malloc(rgbaSize);
            if (rgba) {
                for (int y = 0; y < height; y++) {
                    uint8_t* srcRow = baseAddress + y * bytesPerRow;
                    uint8_t* dstRow = rgba + y * width * 4;
                    for (int x = 0; x < width; x++) {
                        dstRow[x*4+0] = srcRow[x*4+2]; // R
                        dstRow[x*4+1] = srcRow[x*4+1]; // G
                        dstRow[x*4+2] = srcRow[x*4+0]; // B
                        dstRow[x*4+3] = srcRow[x*4+3]; // A
                    }
                }
                self.previewCallback(rgba, width, height, self.userData);
                free(rgba);
            }
        }

        CVPixelBufferUnlockBaseAddress(imageBuffer, kCVPixelBufferLock_ReadOnly);
    }

    self.frameCount++;
}

@end

fennec_capture* fennec_capture_create(const char* target_id, int max_w, int max_h,
    int bitrate_kbps, int fps,
    fennec_nal_callback nal_cb, fennec_frame_callback preview_cb, void* user_data) {

    fennec_capture* cap = calloc(1, sizeof(fennec_capture));
    if (!cap) return NULL;

    cap->target_id = strdup(target_id);
    cap->max_w = max_w;
    cap->max_h = max_h;
    cap->bitrate_kbps = bitrate_kbps;
    cap->fps = fps;

    cap->delegate = [[FennecCaptureDelegate alloc] init];
    cap->delegate.nalCallback = nal_cb;
    cap->delegate.previewCallback = preview_cb;
    cap->delegate.userData = user_data;
    cap->delegate.fps = fps;
    cap->delegate.previewInterval = 1; // preview every frame (lightweight — no encoding on C# side)
    cap->delegate.frameCount = 0;

    cap->queue = dispatch_queue_create("com.fennec.capture", DISPATCH_QUEUE_SERIAL);

    return cap;
}

fennec_status fennec_capture_start(fennec_capture* cap) {
    if (!cap) return FENNEC_ERR_INIT;

    __block fennec_status result = FENNEC_OK;
    dispatch_semaphore_t sem = dispatch_semaphore_create(0);

    [SCShareableContent getShareableContentWithCompletionHandler:^(SCShareableContent* content, NSError* error) {
        if (error || !content) {
            result = FENNEC_ERR_INIT;
            dispatch_semaphore_signal(sem);
            return;
        }

        NSString* targetId = [NSString stringWithUTF8String:cap->target_id];
        SCContentFilter* filter = nil;

        if ([targetId hasPrefix:@"display:"]) {
            uint32_t displayId = (uint32_t)[[targetId substringFromIndex:8] integerValue];
            for (SCDisplay* d in content.displays) {
                if (d.displayID == displayId) {
                    filter = [[SCContentFilter alloc] initWithDisplay:d excludingWindows:@[]];
                    break;
                }
            }
        } else if ([targetId hasPrefix:@"window:"]) {
            uint32_t windowId = (uint32_t)[[targetId substringFromIndex:7] integerValue];
            for (SCWindow* w in content.windows) {
                if (w.windowID == windowId) {
                    filter = [[SCContentFilter alloc] initWithDesktopIndependentWindow:w];
                    break;
                }
            }
        }

        if (!filter) {
            result = FENNEC_ERR_INIT;
            dispatch_semaphore_signal(sem);
            return;
        }

        cap->filter = filter;

        SCStreamConfiguration* config = [[SCStreamConfiguration alloc] init];
        config.width = cap->max_w;
        config.height = cap->max_h;
        config.minimumFrameInterval = CMTimeMake(1, cap->fps);
        config.pixelFormat = kCVPixelFormatType_32BGRA;
        config.showsCursor = YES;
        config.queueDepth = 8;

        // Create VideoToolbox compression session using shared helper
        OSStatus vtStatus = fennec_vt_create_session(cap->max_w, cap->max_h,
            cap->bitrate_kbps, cap->fps, cap->delegate, &cap->session);

        if (vtStatus != noErr) {
            result = FENNEC_ERR_INIT;
            dispatch_semaphore_signal(sem);
            return;
        }

        cap->delegate.compressionSession = cap->session;

        // Create and start SCStream
        cap->stream = [[SCStream alloc] initWithFilter:filter configuration:config delegate:nil];

        NSError* addOutputError = nil;
        [cap->stream addStreamOutput:cap->delegate type:SCStreamOutputTypeScreen
            sampleHandlerQueue:cap->queue error:&addOutputError];

        if (addOutputError) {
            result = FENNEC_ERR_INIT;
            dispatch_semaphore_signal(sem);
            return;
        }

        [cap->stream startCaptureWithCompletionHandler:^(NSError* startError) {
            if (startError) {
                result = FENNEC_ERR_INIT;
            }
            dispatch_semaphore_signal(sem);
        }];
    }];

    dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_SEC));
    return result;
}

fennec_status fennec_capture_stop(fennec_capture* cap) {
    if (!cap || !cap->stream) return FENNEC_ERR_INIT;

    dispatch_semaphore_t sem = dispatch_semaphore_create(0);
    __block fennec_status result = FENNEC_OK;

    [cap->stream stopCaptureWithCompletionHandler:^(NSError* error) {
        if (error) result = FENNEC_ERR_INIT;
        dispatch_semaphore_signal(sem);
    }];

    dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, 5 * NSEC_PER_SEC));

    if (cap->session) {
        VTCompressionSessionCompleteFrames(cap->session, kCMTimeInvalid);
        VTCompressionSessionInvalidate(cap->session);
        CFRelease(cap->session);
        cap->session = NULL;
    }

    return result;
}

fennec_status fennec_capture_update_bitrate(fennec_capture* cap, int bitrate_kbps) {
    if (!cap || !cap->session) return FENNEC_ERR_INIT;
    cap->bitrate_kbps = bitrate_kbps;
    return fennec_vt_update_bitrate(cap->session, bitrate_kbps);
}

fennec_status fennec_capture_update_fps(fennec_capture* cap, int fps) {
    if (!cap || !cap->stream) return FENNEC_ERR_INIT;

    cap->fps = fps;
    cap->delegate.fps = fps;

    // FPS requires stopping SCStream, updating config, and restarting
    __block fennec_status result = FENNEC_OK;
    dispatch_semaphore_t sem = dispatch_semaphore_create(0);

    [cap->stream stopCaptureWithCompletionHandler:^(NSError* error) {
        if (error) { result = FENNEC_ERR_INIT; dispatch_semaphore_signal(sem); return; }

        SCStreamConfiguration* config = [[SCStreamConfiguration alloc] init];
        config.width = cap->max_w;
        config.height = cap->max_h;
        config.minimumFrameInterval = CMTimeMake(1, fps);
        config.pixelFormat = kCVPixelFormatType_32BGRA;
        config.showsCursor = YES;
        config.queueDepth = 3;

        [cap->stream updateConfiguration:config completionHandler:^(NSError* updateError) {
            if (updateError) { result = FENNEC_ERR_INIT; dispatch_semaphore_signal(sem); return; }

            [cap->stream startCaptureWithCompletionHandler:^(NSError* startError) {
                if (startError) result = FENNEC_ERR_INIT;
                dispatch_semaphore_signal(sem);
            }];
        }];
    }];

    dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_SEC));
    return result;
}

void fennec_capture_destroy(fennec_capture* cap) {
    if (!cap) return;

    if (cap->stream) {
        fennec_capture_stop(cap);
    }

    free(cap->target_id);
    free(cap);
}
