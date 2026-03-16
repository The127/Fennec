#import <ScreenCaptureKit/ScreenCaptureKit.h>
#import <VideoToolbox/VideoToolbox.h>
#import <CoreMedia/CoreMedia.h>
#import <CoreVideo/CoreVideo.h>
#import <AppKit/AppKit.h>
#include "fennec_video.h"
#include "fennec_capture_internal.h"
#include <stdlib.h>

// --- Picker (macOS 14+) ---

struct fennec_picker {
    FennecCaptureDelegate* delegate;
    VTCompressionSessionRef session;
    SCStream* stream;
    dispatch_queue_t queue;
    int max_w;
    int max_h;
    int bitrate_kbps;
    int fps;
    fennec_nal_callback nal_cb;
    fennec_frame_callback preview_cb;
    fennec_picker_selected_callback on_selected;
    fennec_picker_cancelled_callback on_cancelled;
    void* user_data;
    id observer;  // FennecPickerObserver (held strongly)
};

int fennec_picker_is_available(void) {
    if (@available(macOS 14, *)) {
        return 1;
    }
    return 0;
}

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunguarded-availability-new"

@interface FennecPickerObserver : NSObject <SCContentSharingPickerObserver, SCStreamDelegate>
@property (nonatomic) fennec_picker* picker;
@property (nonatomic) BOOL didFireStop;
@end

@implementation FennecPickerObserver

- (void)contentSharingPickerStartDidFailWithError:(NSError *)error {
    NSLog(@"fennec_picker: picker start failed: %@", error);
    if (self.picker && self.picker->on_cancelled) {
        self.picker->on_cancelled(self.picker->user_data);
    }
}

- (void)contentSharingPicker:(SCContentSharingPicker *)picker didCancelForStream:(SCStream *)stream {
    if (!self.picker || self.didFireStop) return;
    self.didFireStop = YES;

    // Stop any active stream
    if (self.picker->stream) {
        [self.picker->stream stopCaptureWithCompletionHandler:^(NSError* error) {}];
        self.picker->stream = nil;
    }

    // Tear down VT session
    if (self.picker->session) {
        VTCompressionSessionCompleteFrames(self.picker->session, kCMTimeInvalid);
        VTCompressionSessionInvalidate(self.picker->session);
        CFRelease(self.picker->session);
        self.picker->session = NULL;
    }

    if (self.picker->on_cancelled) {
        self.picker->on_cancelled(self.picker->user_data);
    }
}

// SCStreamDelegate — fires when macOS stops the stream (e.g. system stop button)
- (void)stream:(SCStream *)stream didStopWithError:(NSError *)error {
    NSLog(@"fennec_picker: stream stopped with error: %@", error);
    if (!self.picker || self.didFireStop) return;
    self.didFireStop = YES;

    // Tear down VT session
    if (self.picker->session) {
        VTCompressionSessionCompleteFrames(self.picker->session, kCMTimeInvalid);
        VTCompressionSessionInvalidate(self.picker->session);
        CFRelease(self.picker->session);
        self.picker->session = NULL;
    }

    self.picker->stream = nil;

    if (self.picker->on_cancelled) {
        self.picker->on_cancelled(self.picker->user_data);
    }
}

- (void)contentSharingPicker:(SCContentSharingPicker *)picker didUpdateWithFilter:(SCContentFilter *)filter forStream:(SCStream *)stream {
    if (!self.picker) return;

    if (!stream) {
        // First selection — create VT session + SCStream and start capture
        [self startCaptureWithFilter:filter];
    } else {
        // Mid-share switch — update the existing stream's content filter
        [self.picker->stream updateContentFilter:filter completionHandler:^(NSError* error) {
            if (error) {
                NSLog(@"fennec_picker: failed to update content filter: %@", error);
            }
        }];
    }
}

- (void)startCaptureWithFilter:(SCContentFilter *)filter {
    fennec_picker* p = self.picker;
    self.didFireStop = NO;

    // Create delegate
    p->delegate = [[FennecCaptureDelegate alloc] init];
    p->delegate.nalCallback = p->nal_cb;
    p->delegate.previewCallback = p->preview_cb;
    p->delegate.userData = p->user_data;
    p->delegate.fps = p->fps;
    p->delegate.previewInterval = 1;
    p->delegate.frameCount = 0;

    // Create VT compression session
    OSStatus vtStatus = fennec_vt_create_session(p->max_w, p->max_h,
        p->bitrate_kbps, p->fps, p->delegate, &p->session);

    if (vtStatus != noErr) {
        NSLog(@"fennec_picker: failed to create VT session: %d", (int)vtStatus);
        if (p->on_cancelled) p->on_cancelled(p->user_data);
        return;
    }

    p->delegate.compressionSession = p->session;

    // Configure SCStream
    SCStreamConfiguration* config = [[SCStreamConfiguration alloc] init];
    config.width = p->max_w;
    config.height = p->max_h;
    config.minimumFrameInterval = CMTimeMake(1, p->fps);
    config.pixelFormat = kCVPixelFormatType_32BGRA;
    config.showsCursor = YES;
    config.queueDepth = 8;

    p->stream = [[SCStream alloc] initWithFilter:filter configuration:config delegate:(FennecPickerObserver*)p->observer];

    NSError* addOutputError = nil;
    [p->stream addStreamOutput:p->delegate type:SCStreamOutputTypeScreen
        sampleHandlerQueue:p->queue error:&addOutputError];

    if (addOutputError) {
        NSLog(@"fennec_picker: failed to add stream output: %@", addOutputError);
        VTCompressionSessionInvalidate(p->session);
        CFRelease(p->session);
        p->session = NULL;
        p->stream = nil;
        if (p->on_cancelled) p->on_cancelled(p->user_data);
        return;
    }

    [p->stream startCaptureWithCompletionHandler:^(NSError* startError) {
        if (startError) {
            NSLog(@"fennec_picker: failed to start capture: %@", startError);
            VTCompressionSessionInvalidate(p->session);
            CFRelease(p->session);
            p->session = NULL;
            p->stream = nil;
            if (p->on_cancelled) p->on_cancelled(p->user_data);
            return;
        }

        if (p->on_selected) {
            p->on_selected(p->user_data);
        }
    }];
}

@end

fennec_picker* fennec_picker_create(
    int max_w, int max_h, int bitrate_kbps, int fps,
    fennec_nal_callback nal_cb, fennec_frame_callback preview_cb,
    fennec_picker_selected_callback on_selected,
    fennec_picker_cancelled_callback on_cancelled,
    void* user_data) {

    if (@available(macOS 14, *)) {
        fennec_picker* p = calloc(1, sizeof(fennec_picker));
        if (!p) return NULL;

        p->max_w = max_w;
        p->max_h = max_h;
        p->bitrate_kbps = bitrate_kbps;
        p->fps = fps;
        p->nal_cb = nal_cb;
        p->preview_cb = preview_cb;
        p->on_selected = on_selected;
        p->on_cancelled = on_cancelled;
        p->user_data = user_data;
        p->queue = dispatch_queue_create("com.fennec.picker", DISPATCH_QUEUE_SERIAL);

        FennecPickerObserver* obs = [[FennecPickerObserver alloc] init];
        obs.picker = p;
        p->observer = obs;

        SCContentSharingPicker* picker = [SCContentSharingPicker sharedPicker];

        // Configure: exclude own app, allow screens + windows
        SCContentSharingPickerConfiguration* pickerConfig = [[SCContentSharingPickerConfiguration alloc] init];
        NSString* bundleId = [[NSBundle mainBundle] bundleIdentifier];
        if (bundleId) {
            SCRunningApplication* selfApp = nil;
            // Find our own running application to exclude
            dispatch_semaphore_t sem = dispatch_semaphore_create(0);
            [SCShareableContent getShareableContentWithCompletionHandler:^(SCShareableContent* content, NSError* error) {
                if (!error && content) {
                    for (SCRunningApplication* app in content.applications) {
                        if ([app.bundleIdentifier isEqualToString:bundleId]) {
                            pickerConfig.excludedBundleIDs = @[bundleId];
                            break;
                        }
                    }
                }
                dispatch_semaphore_signal(sem);
            }];
            dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, 3 * NSEC_PER_SEC));
        }
        pickerConfig.allowedPickerModes = SCContentSharingPickerModeSingleWindow | SCContentSharingPickerModeSingleDisplay;
        picker.defaultConfiguration = pickerConfig;

        [picker addObserver:obs];

        return p;
    }

    return NULL;
}

fennec_status fennec_picker_activate(fennec_picker* picker) {
    if (!picker) return FENNEC_ERR_INIT;

    if (@available(macOS 14, *)) {
        SCContentSharingPicker* systemPicker = [SCContentSharingPicker sharedPicker];
        systemPicker.active = YES;
        [systemPicker present];
        return FENNEC_OK;
    }

    return FENNEC_ERR_INIT;
}

fennec_status fennec_picker_stop(fennec_picker* picker) {
    if (!picker) return FENNEC_ERR_INIT;

    if (@available(macOS 14, *)) {
        SCContentSharingPicker* systemPicker = [SCContentSharingPicker sharedPicker];
        systemPicker.active = NO;
    }

    if (picker->stream) {
        dispatch_semaphore_t sem = dispatch_semaphore_create(0);
        [picker->stream stopCaptureWithCompletionHandler:^(NSError* error) {
            dispatch_semaphore_signal(sem);
        }];
        dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, 5 * NSEC_PER_SEC));
        picker->stream = nil;
    }

    if (picker->session) {
        VTCompressionSessionCompleteFrames(picker->session, kCMTimeInvalid);
        VTCompressionSessionInvalidate(picker->session);
        CFRelease(picker->session);
        picker->session = NULL;
    }

    return FENNEC_OK;
}

fennec_status fennec_picker_update_bitrate(fennec_picker* picker, int bitrate_kbps) {
    if (!picker) return FENNEC_ERR_INIT;
    picker->bitrate_kbps = bitrate_kbps;
    return fennec_vt_update_bitrate(picker->session, bitrate_kbps);
}

fennec_status fennec_picker_update_fps(fennec_picker* picker, int fps) {
    if (!picker || !picker->stream) return FENNEC_ERR_INIT;

    picker->fps = fps;
    if (picker->delegate) picker->delegate.fps = fps;

    // FPS requires stopping SCStream, updating config, and restarting
    __block fennec_status result = FENNEC_OK;
    dispatch_semaphore_t sem = dispatch_semaphore_create(0);

    [picker->stream stopCaptureWithCompletionHandler:^(NSError* error) {
        if (error) { result = FENNEC_ERR_INIT; dispatch_semaphore_signal(sem); return; }

        SCStreamConfiguration* config = [[SCStreamConfiguration alloc] init];
        config.width = picker->max_w;
        config.height = picker->max_h;
        config.minimumFrameInterval = CMTimeMake(1, fps);
        config.pixelFormat = kCVPixelFormatType_32BGRA;
        config.showsCursor = YES;
        config.queueDepth = 8;

        [picker->stream updateConfiguration:config completionHandler:^(NSError* updateError) {
            if (updateError) { result = FENNEC_ERR_INIT; dispatch_semaphore_signal(sem); return; }

            [picker->stream startCaptureWithCompletionHandler:^(NSError* startError) {
                if (startError) result = FENNEC_ERR_INIT;
                dispatch_semaphore_signal(sem);
            }];
        }];
    }];

    dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_SEC));
    return result;
}

fennec_status fennec_picker_update_size(fennec_picker* picker, int max_w, int max_h) {
    if (!picker || !picker->stream) return FENNEC_ERR_INIT;

    picker->max_w = max_w;
    picker->max_h = max_h;

    // Size requires stopping SCStream, recreating VT session at new dimensions, and restarting
    __block fennec_status result = FENNEC_OK;
    dispatch_semaphore_t sem = dispatch_semaphore_create(0);

    [picker->stream stopCaptureWithCompletionHandler:^(NSError* error) {
        if (error) { result = FENNEC_ERR_INIT; dispatch_semaphore_signal(sem); return; }

        // Tear down old VT session
        if (picker->session) {
            VTCompressionSessionCompleteFrames(picker->session, kCMTimeInvalid);
            VTCompressionSessionInvalidate(picker->session);
            CFRelease(picker->session);
            picker->session = NULL;
        }

        // Create new VT session at new dimensions
        OSStatus vtStatus = fennec_vt_create_session(max_w, max_h,
            picker->bitrate_kbps, picker->fps, picker->delegate, &picker->session);
        if (vtStatus != noErr) {
            result = FENNEC_ERR_INIT;
            dispatch_semaphore_signal(sem);
            return;
        }
        picker->delegate.compressionSession = picker->session;

        SCStreamConfiguration* config = [[SCStreamConfiguration alloc] init];
        config.width = max_w;
        config.height = max_h;
        config.minimumFrameInterval = CMTimeMake(1, picker->fps);
        config.pixelFormat = kCVPixelFormatType_32BGRA;
        config.showsCursor = YES;
        config.queueDepth = 8;

        [picker->stream updateConfiguration:config completionHandler:^(NSError* updateError) {
            if (updateError) { result = FENNEC_ERR_INIT; dispatch_semaphore_signal(sem); return; }

            [picker->stream startCaptureWithCompletionHandler:^(NSError* startError) {
                if (startError) result = FENNEC_ERR_INIT;
                dispatch_semaphore_signal(sem);
            }];
        }];
    }];

    dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_SEC));
    return result;
}

void fennec_picker_destroy(fennec_picker* picker) {
    if (!picker) return;

    fennec_picker_stop(picker);

    if (@available(macOS 14, *)) {
        if (picker->observer) {
            SCContentSharingPicker* systemPicker = [SCContentSharingPicker sharedPicker];
            [systemPicker removeObserver:(FennecPickerObserver*)picker->observer];
            ((FennecPickerObserver*)picker->observer).picker = nil;
            picker->observer = nil;
        }
    }

    picker->delegate = nil;
    free(picker);
}

#pragma clang diagnostic pop
