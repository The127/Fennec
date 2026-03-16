#ifndef FENNEC_CAPTURE_INTERNAL_H
#define FENNEC_CAPTURE_INTERNAL_H

#import <ScreenCaptureKit/ScreenCaptureKit.h>
#import <VideoToolbox/VideoToolbox.h>
#import <CoreMedia/CoreMedia.h>
#import <CoreVideo/CoreVideo.h>
#include "fennec_video.h"

// Shared FennecCaptureDelegate — conforms to SCStreamOutput, feeds frames to VT encoder
@interface FennecCaptureDelegate : NSObject <SCStreamOutput>
@property (nonatomic) VTCompressionSessionRef compressionSession;
@property (nonatomic) fennec_nal_callback nalCallback;
@property (nonatomic) fennec_frame_callback previewCallback;
@property (nonatomic) void* userData;
@property (nonatomic) int64_t frameCount;
@property (nonatomic) int fps;
@property (nonatomic) int previewInterval;
@end

// VT compression output callback — shared between capture and picker
void fennec_vt_compression_output_callback(void* outputCallbackRefCon,
    void* sourceFrameRefCon, OSStatus status, VTEncodeInfoFlags infoFlags,
    CMSampleBufferRef sampleBuffer);

// Create and configure a VT compression session for low-latency H.264
OSStatus fennec_vt_create_session(int width, int height, int bitrate_kbps, int fps,
    FennecCaptureDelegate* delegate, VTCompressionSessionRef* outSession);

// Update bitrate on an existing VT session
fennec_status fennec_vt_update_bitrate(VTCompressionSessionRef session, int bitrate_kbps);

#endif // FENNEC_CAPTURE_INTERNAL_H
