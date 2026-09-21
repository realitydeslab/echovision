// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <botao.a.hu@gmail.com>
// SPDX-License-Identifier: MIT

#import "VideoRecorder.h"

#include <TargetConditionals.h>

#if TARGET_OS_IOS
#import <UIKit/UIKit.h>
#endif

#import <AVFoundation/AVFoundation.h>
#import <Metal/Metal.h>
#import <CoreVideo/CVMetalTextureCache.h>
#import <CoreMedia/CMBlockBuffer.h>
#import <Accelerate/Accelerate.h>
#import <AudioToolbox/AudioToolbox.h>

@interface VideoRecorder ()

@property (nonatomic, strong) AVAssetWriter* writer;
@property (nonatomic, strong) AVAssetWriterInput* videoWriterInput;
@property (nonatomic, strong) AVAssetWriterInput* audioWriterInput;
@property (nonatomic, strong) AVAssetWriterInputPixelBufferAdaptor* bufferAdaptor;
@property (assign) AudioStreamBasicDescription audioFormat;
@property (assign) CMFormatDescriptionRef cmFormat;
@property (nonatomic, strong) AVCaptureSession* captureSession;
@property (assign) bool isRecording;
@property (assign) int width;
@property (assign) int height;
@property (nonatomic, strong) AVCaptureDevice* audioDeviceMic;
@property (nonatomic, strong) AVCaptureDeviceInput* audioInputMic;
@property (nonatomic, strong) AVAudioEngine* audioEngine;
@property (nonatomic, strong) AVCaptureAudioDataOutput* audioOutput;

// --- zero-copy GPU path ---
// NOTE: deliberately NOT named `metalDevice` -- that would synthesise a setter
// called `setMetalDevice:`, which collides with the public method of the same name
// and makes it recurse into itself.
@property (nonatomic, strong) id<MTLDevice> currentMetalDevice;
@property (assign) CVMetalTextureCacheRef metalTextureCache;
/// AVAssetWriterInput is not thread-safe and the GPU completion handler fires on an
/// arbitrary thread, so every append is funnelled through this serial queue.
@property (nonatomic, strong) dispatch_queue_t appendQueue;

// Slot-based GPU path. Parallel arrays, one entry per slot.
@property (assign) CVPixelBufferRef* slotBuffers;
@property (assign) CVMetalTextureRef* slotCVTextures;
@property (nonatomic, strong) NSMutableArray<id<MTLTexture>>* slotTextures;
@property (assign) int slotCount;

@end

@implementation VideoRecorder

- (instancetype)init {
    if (self = [super init]) {
    }
    return self;
}

+ (id)sharedInstance {
    static dispatch_once_t onceToken = 0;
    static id _sharedObject = nil;
    dispatch_once(&onceToken, ^{
        _sharedObject = [[self alloc] init];
    });
    return _sharedObject;
}

// - (void) startMicrophone {
//     self.captureSession = [[AVCaptureSession alloc] init];
//     [self.captureSession setSessionPreset:AVCaptureSessionPresetHigh];
//     self.audioDeviceMic = [AVCaptureDevice defaultDeviceWithMediaType:AVMediaTypeAudio];
//     self.audioInputMic = [AVCaptureDeviceInput deviceInputWithDevice:self.audioDeviceMic error:nil];
//     if ([self.captureSession canAddInput:self.audioInputMic]) {
//         [self.captureSession addInput:self.audioInputMic];
//     }
//     self.audioOutput = [[AVCaptureAudioDataOutput alloc] init];
//     [self.audioOutput setSampleBufferDelegate:self queue:dispatch_get_main_queue()];
//      if ([self.captureSession canAddOutput:self.audioOutput]) {
//         [self.captureSession addOutput:self.audioOutput];
//     }

//     // Initialize AVAudioEngine
//     self.audioEngine = [[AVAudioEngine alloc] init];
//     self.outputNode = [self.audioEngine outputNode];
//     [self.outputNode setVoiceProcessingEnabled:YES];
//     self.outputFormat = [outputNode inputFormatForBus: 0];
//     playerNode = [[AVAudioPlayerNode alloc] init];
//     [self.audioEngine attachNode: playerNode];
//     AVAudioMixerNode *mixerNode = [self.audioEngine mainMixerNode];
//     [self.audioEngine connect:inputNode to:mixerNode format:inputFormat];
//     NSArray<AVAudioSessionPortDescription *> *inputPorts = [audioSession availableInputs];

//     for (AVAudioSessionPortDescription *port in inputPorts) {
//         if ([port.portType isEqualToString:AVAudioSessionPortUSBAudio]) {
//             usbPort = port;
//             break;
//         }
//     }
//     NSError *inputError = nil;
//     [audioSession setPreferredInput:usbPort error:&inputError];


//     [self.outputNode installTapOnBus:0
//           bufferSize:1024
//           format:recordingFormat
//           block:^(AVAudioPCMBuffer *buffer, AVAudioTime *when) {
//                    [request appendBuffer:buffer];
                   
//     }];
    
//     [self.audioEngine prepare];
//     [self.audioEngine start];

//     [engine connect: playerNode to: mainMixerNode format: outputFormat];

//     // Create AVAudioPlayerNodes for each microphone
//     AVAudioPlayerNode *playerNode1 = [[AVAudioPlayerNode alloc] init];

// //    self.mixerNode = [self.audioEngine mainMixerNode];
//     [self.audioEngine attachNode:playerNode1];
//     [self.audioEngine attachNode:playerNode2];
    
// //    [self.audioEngine connect:playerNode1 to:self.mixerNode format:nil];
// //    [self.audioEngine connect:playerNode2 to:self.mixerNode format:nil];
// //    NSError *audioEngineError;
// //    [self.audioEngine startAndReturnError:&audioEngineError];
// //    if (audioEngineError) {
// //        NSLog(@"Error starting AVAudioEngine: %@", audioEngineError.localizedDescription);
// //    }
// //
//    AVAudioFormat *audioOutputFormat = [self.audioEngine.outputNode outputFormatForBus:0];
//    CMFormatDescriptionRef audioFormatDescription = CMAudioFormatDescriptionCreate(audioOutputFormat.streamDescription,
//        sizeof(AudioStreamBasicDescription), kCMAudioFormatFlags_IsFloat);

//     // Start AVCaptureSession
//     [self.captureSession startRunning];
// }

- (int)startRecording:(const char *)filePath width:(int)width height:(int)height audioSampleRate:(float)audioSampleRate audioChannelCount:(int)audioChannelCount
{
    if (self.writer)
    {
        NSLog(@"Recording has already been initiated.");
        return -1;
    }
    
    // Asset writer setup
    NSURL* filePathURL = [NSURL fileURLWithPath:[NSString stringWithUTF8String:filePath]];

    // Delete existing file if present — AVAssetWriter fails if file already exists
    NSFileManager *fm = [NSFileManager defaultManager];
    if ([fm fileExistsAtPath:filePathURL.path]) {
        [fm removeItemAtURL:filePathURL error:nil];
    }

    NSError* err;
    self.writer = [[AVAssetWriter alloc] initWithURL: filePathURL
                                        fileType: AVFileTypeMPEG4
                                           error: &err];
    
    if (err)
    {
        NSLog(@"Failed to initialize AVAssetWriter (%@)", err);
        return -1;
    }
    
    // Without an explicit bitrate AVFoundation picks its own, which measured
    // 4.5 Mbps at 2768x1280 -- high resolution, soft picture. Scale the target
    // with the pixel count so a 4K capture is not starved and a small one is not
    // wasteful, at roughly 0.11 bits per pixel per frame at 60 fps.
    const int frameRate = 60;
    long long bitRate = (long long)width * height * 7LL;
    if (bitRate < 8000000LL)  { bitRate = 8000000LL; }
    if (bitRate > 60000000LL) { bitRate = 60000000LL; }

    NSDictionary* compression = @{
        AVVideoAverageBitRateKey:          @(bitRate),
        AVVideoExpectedSourceFrameRateKey: @(frameRate),
        AVVideoMaxKeyFrameIntervalKey:     @(frameRate),
        // No AVVideoProfileLevelKey: AVFoundation only defines H.264 profile
        // constants; the HEVC ones live in VideoToolbox. Left unset, the encoder
        // picks an appropriate HEVC profile and level for the resolution.
    };

    // The project renders in Linear colour space and the frames arrive as sRGB
    // bytes. Tagging them BT.709 stops players guessing; untagged output probed
    // as color_space=unknown.
    NSDictionary* colorProperties = @{
        AVVideoColorPrimariesKey:   AVVideoColorPrimaries_ITU_R_709_2,
        AVVideoTransferFunctionKey: AVVideoTransferFunction_ITU_R_709_2,
        AVVideoYCbCrMatrixKey:      AVVideoYCbCrMatrix_ITU_R_709_2,
    };

    NSDictionary* settings = @{
        AVVideoCodecKey: AVVideoCodecTypeHEVC,
        AVVideoWidthKey: @(width),
        AVVideoHeightKey: @(height),
        AVVideoCompressionPropertiesKey: compression,
        AVVideoColorPropertiesKey: colorProperties };
    
    self.videoWriterInput = [AVAssetWriterInput assetWriterInputWithMediaType: AVMediaTypeVideo outputSettings: settings];
    self.videoWriterInput.expectsMediaDataInRealTime = YES;

    // Pixel buffer adaptor setup
    NSDictionary* attribs = @{
        (NSString*)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
        (NSString*)kCVPixelBufferWidthKey: @(width),
        (NSString*)kCVPixelBufferHeightKey: @(height),
        // Required for the zero-copy GPU path: the buffer must be IOSurface-backed so
        // CVMetalTextureCache can wrap it as an MTLTexture sharing the same memory.
        (NSString*)kCVPixelBufferMetalCompatibilityKey: @YES,
        (NSString*)kCVPixelBufferIOSurfacePropertiesKey: @{}
    };
    
    self.bufferAdaptor = [AVAssetWriterInputPixelBufferAdaptor
                      assetWriterInputPixelBufferAdaptorWithAssetWriterInput: self.videoWriterInput
                      sourcePixelBufferAttributes: attribs];
    
    // Audio writer input setup
    NSDictionary* audioSettings = @{
        AVFormatIDKey: @(kAudioFormatMPEG4AAC),
        AVSampleRateKey: @(audioSampleRate),
        AVNumberOfChannelsKey: @(audioChannelCount),
        AVEncoderAudioQualityKey: @(AVAudioQualityHigh)
    };
    self.audioWriterInput = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeAudio
                         outputSettings:audioSettings];
    self.audioWriterInput.expectsMediaDataInRealTime = YES;
    
    AudioStreamBasicDescription audioFormat;
    audioFormat = {0};
    audioFormat.mSampleRate = audioSampleRate; // Sample rate, 44100Hz is CD quality
    audioFormat.mFormatID = kAudioFormatLinearPCM; // Specify the data format to be PCM
    audioFormat.mFormatFlags = kLinearPCMFormatFlagIsFloat; // Flags specific for the format
    audioFormat.mFramesPerPacket = 1; // Each packet contains one frame for PCM data
    audioFormat.mChannelsPerFrame = (uint32_t) audioChannelCount; // Set the number of channels
    audioFormat.mBitsPerChannel = sizeof(float) * 8; // Number of bits per channel, 32 for float
    audioFormat.mBytesPerFrame = (uint32_t) audioChannelCount * sizeof(float); // Bytes per frame
    audioFormat.mBytesPerPacket = audioFormat.mBytesPerFrame * audioFormat.mFramesPerPacket; // Bytes per packet
    audioFormat.mReserved = 0;
    self.audioFormat = audioFormat;
    
    CMFormatDescriptionRef cmFormat;
    OSStatus status = CMAudioFormatDescriptionCreate(kCFAllocatorDefault,
                                   &audioFormat,
                                   0,
                                   NULL,
                                   0,
                                   NULL,
                                   NULL,
                                   &cmFormat
                                   );
    
    if (status != noErr) {
        NSLog(@"CMAudioFormatDescriptionCreate hasn't been initiated.");
        return -1;
    }
    self.cmFormat = cmFormat;
   
    [self.writer addInput:self.videoWriterInput];
    [self.writer addInput:self.audioWriterInput];
    
    // Recording start
    if (![self.writer startWriting])
    {
        NSLog(@"Failed to start (%ld: %@)", self.writer.status, self.writer.error);
        return -1;
    }

    self.width = width;
    self.height = height;
    [self.writer startSessionAtSourceTime:kCMTimeZero];

    // Check writer status after starting session
    if (self.writer.status == AVAssetWriterStatusFailed) {
        NSLog(@"Writer failed after starting session: %@", self.writer.error);
        return -1;
    }

    if (!self.bufferAdaptor.pixelBufferPool) {
        NSLog(@"pixelBufferPool is nil after starting session. Writer status: %ld, error: %@",
              (long)self.writer.status, self.writer.error);
        return -1;
    }

    if (self.appendQueue == nil) {
        self.appendQueue = dispatch_queue_create("io.holokit.videorecorder.append",
                                                 DISPATCH_QUEUE_SERIAL);
    }

    self.isRecording = YES;
    NSLog(@"Recording started: %dx%d, writer status: %ld, gpuPath: %@",
          width, height, (long)self.writer.status,
          self.metalTextureCache != NULL ? @"yes" : @"no");

    return 0;
}

- (int)appendAudioFrame:(void *)source size:(int)size time:(double)time
{
    if (!self.isRecording) {
        return -2;
    }

    if (!self.writer)
    {
        NSLog(@"Recording hasn't been initiated.");
        return -1;
    }

    if (!self.audioWriterInput.isReadyForMoreMediaData)
    {
        NSLog(@"Audio Writer is not ready.");
        return -1;
    }

    // Write _audioInputWriter with buffer

    CMTime presentationTimestamp = CMTimeMakeWithSeconds(time, 240); // Adjust timescale as needed
    
    CMBlockBufferRef blockBuffer;
    OSStatus status = CMBlockBufferCreateWithMemoryBlock(kCFAllocatorDefault, NULL, size,
                                                         kCFAllocatorDefault, NULL, 0, size,
                                                         kCMBlockBufferAssureMemoryNowFlag, &blockBuffer);
    
    if (status != kCMBlockBufferNoErr) {
        NSLog(@"CMBlockBufferCreateWithMemoryBlock error");
        return -1;
    }
    
    status = CMBlockBufferReplaceDataBytes(source, blockBuffer, 0, size);
    
    if (status != kCMBlockBufferNoErr) {
        NSLog(@"CMBlockBufferReplaceDataBytes error");
        CFRelease(blockBuffer);
        return -1;
    }

    CMItemCount nSamples = size / self.audioFormat.mBytesPerFrame;
    
    CMSampleBufferRef sampleBuffer;
    status = CMAudioSampleBufferCreateReadyWithPacketDescriptions(kCFAllocatorDefault,
                                                                blockBuffer,
                                                                self.cmFormat,
                                                                nSamples,
                                                                presentationTimestamp,
                                                                NULL,
                                                                &sampleBuffer);
    
    if (status != noErr) {
        CFRelease(blockBuffer);
        return -1;
    }

    if (!CMSampleBufferDataIsReady(sampleBuffer))
    {
        NSLog(@"sample buffer is not ready");
        CFRelease(sampleBuffer);
        CFRelease(blockBuffer);
        return -1;
    }
   
    if (!CMSampleBufferIsValid(sampleBuffer))
    {
        NSLog(@"Audio sample buffer is not valid");
        CFRelease(sampleBuffer);
        CFRelease(blockBuffer);
        return -1;
    }
        
    bool result = [self.audioWriterInput appendSampleBuffer:sampleBuffer];
    if (!result) {
        NSLog(@"appendSampleBuffer error. Writer status: %ld, error: %@",
              (long)self.writer.status, self.writer.error);
        CFRelease(sampleBuffer);
        CFRelease(blockBuffer);
        return -1;
    }
    
    CFRelease(sampleBuffer);
    CFRelease(blockBuffer);
    return 0;
}

- (int)appendVideoFrame:(void *)source size:(int)size time:(double)time
{
    if (!self.isRecording) {
        return -2;
    }

    if (!self.writer)
    {
        NSLog(@"Recording hasn't been initiated.");
        return -1;
    }
    
    if (!self.videoWriterInput.isReadyForMoreMediaData)
    {
        NSLog(@"Video Writer is not ready.");
        return -1;
    }
    
    if (!self.bufferAdaptor.pixelBufferPool)
    {
        NSLog(@"Video Writer pixelBufferPool is empty. Writer status: %ld, error: %@",
              (long)self.writer.status, self.writer.error);
        return -1;
    }

    // Buffer allocation
    CVPixelBufferRef pixelBuffer = nil;
    CVReturn ret = CVPixelBufferPoolCreatePixelBuffer(NULL, self.bufferAdaptor.pixelBufferPool, &pixelBuffer);
    
    if (ret != kCVReturnSuccess)
    {
        NSLog(@"Can't allocate a pixel buffer (%d)", ret);
        NSLog(@"%ld: %@", self.writer.status, self.writer.error);
        return -1;
    }
    
    // Buffer copy
    CVPixelBufferLockBaseAddress(pixelBuffer, 0);
    void* baseAddress = CVPixelBufferGetBaseAddress(pixelBuffer);
    int bytesPerRow = (int) CVPixelBufferGetBytesPerRow(pixelBuffer);
    int bufferSize = (int) CVPixelBufferGetDataSize(pixelBuffer);
   
    vImage_Buffer srcBuffer;
    srcBuffer.data = source;
    srcBuffer.height = self.height;
    srcBuffer.width = self.width;
    srcBuffer.rowBytes = self.width * 4;

    vImage_Buffer dstBuffer;
    dstBuffer.data = baseAddress;
    dstBuffer.height = self.height;
    dstBuffer.width = self.width;
    dstBuffer.rowBytes = bytesPerRow;

    // NDEBUG strips assert() from Release builds, so a size mismatch used to
    // reach vImage and read past the end of the source buffer in exactly the
    // configuration that ships. Check at runtime instead.
    if (size != self.width * self.height * 4) {
        NSLog(@"[VideoRecorder] Frame size mismatch: got %d, expected %d. Dropping frame.",
              size, self.width * self.height * 4);
        CVPixelBufferUnlockBaseAddress(pixelBuffer, 0);
        CVPixelBufferRelease(pixelBuffer);
        return -1;
    }

    // RGBA to BGRA: swap R (index 0) and B (index 2)
    const uint8_t permuteMap[4] = { 2, 1, 0, 3 };
    vImagePermuteChannels_ARGB8888(&srcBuffer, &dstBuffer, permuteMap, kvImageNoFlags);
    CVPixelBufferUnlockBaseAddress(pixelBuffer, 0);

    // Buffer submission
    [self.bufferAdaptor appendPixelBuffer:pixelBuffer
                    withPresentationTime:CMTimeMakeWithSeconds(time, 240)];
    
    CVPixelBufferRelease(pixelBuffer);

    return 0;
}

- (void)setMetalDevice:(id<MTLDevice>)device
{
    if (self.currentMetalDevice == device) {
        return;
    }
    self.currentMetalDevice = device;

    if (self.metalTextureCache) {
        CFRelease(self.metalTextureCache);
        self.metalTextureCache = NULL;
    }
    if (device == nil) {
        return;
    }

    CVReturn ret = CVMetalTextureCacheCreate(kCFAllocatorDefault, NULL, device, NULL,
                                             &_metalTextureCache);
    if (ret != kCVReturnSuccess) {
        NSLog(@"[VideoRecorder] CVMetalTextureCacheCreate failed (%d); GPU path unavailable.", ret);
        self.metalTextureCache = NULL;
    }
}

#pragma mark - Slot-based GPU path

- (int)allocateGPUSlots:(int)count width:(int)width height:(int)height
{
    [self releaseGPUSlots];

    // iOS has one GPU, so the system default device is the one Unity renders
    // with; textures created from it are shareable with Unity's.
    if (self.currentMetalDevice == nil) {
        id<MTLDevice> device = MTLCreateSystemDefaultDevice();
        if (device == nil) {
            NSLog(@"[VideoRecorder] No Metal device; GPU slots unavailable.");
            return 0;
        }
        [self setMetalDevice:device];
    }
    if (self.metalTextureCache == NULL) {
        NSLog(@"[VideoRecorder] No texture cache; GPU slots unavailable.");
        return 0;
    }

    NSDictionary* attrs = @{
        (NSString*)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
        (NSString*)kCVPixelBufferWidthKey: @(width),
        (NSString*)kCVPixelBufferHeightKey: @(height),
        (NSString*)kCVPixelBufferMetalCompatibilityKey: @YES,
        (NSString*)kCVPixelBufferIOSurfacePropertiesKey: @{}
    };

    self.slotBuffers = (CVPixelBufferRef*)calloc(count, sizeof(CVPixelBufferRef));
    self.slotCVTextures = (CVMetalTextureRef*)calloc(count, sizeof(CVMetalTextureRef));
    self.slotTextures = [NSMutableArray arrayWithCapacity:count];

    int created = 0;
    for (int i = 0; i < count; i++) {
        CVPixelBufferRef pb = NULL;
        if (CVPixelBufferCreate(kCFAllocatorDefault, width, height,
                                kCVPixelFormatType_32BGRA,
                                (__bridge CFDictionaryRef)attrs, &pb) != kCVReturnSuccess) {
            break;
        }

        CVMetalTextureRef cvTex = NULL;
        if (CVMetalTextureCacheCreateTextureFromImage(kCFAllocatorDefault,
                                                      self.metalTextureCache, pb, NULL,
                                                      MTLPixelFormatBGRA8Unorm,
                                                      (size_t)width, (size_t)height, 0,
                                                      &cvTex) != kCVReturnSuccess || cvTex == NULL) {
            CVPixelBufferRelease(pb);
            break;
        }

        id<MTLTexture> tex = CVMetalTextureGetTexture(cvTex);
        if (tex == nil) {
            CFRelease(cvTex);
            CVPixelBufferRelease(pb);
            break;
        }

        self.slotBuffers[i] = pb;
        self.slotCVTextures[i] = cvTex;
        [self.slotTextures addObject:tex];
        created++;
    }

    self.slotCount = created;
    NSLog(@"[VideoRecorder] Allocated %d/%d GPU slots at %dx%d", created, count, width, height);
    return created;
}

- (void*)gpuSlotTexture:(int)index
{
    if (index < 0 || index >= self.slotCount) {
        return NULL;
    }
    return (__bridge void*)self.slotTextures[index];
}

- (int)submitGPUSlot:(int)index time:(double)time
{
    if (!self.isRecording) {
        return -2;
    }
    if (index < 0 || index >= self.slotCount || !self.writer) {
        return -1;
    }
    if (!self.videoWriterInput.isReadyForMoreMediaData) {
        return -3;   // backpressure; caller drops this frame and its timestamp
    }

    CVPixelBufferRef pb = self.slotBuffers[index];
    if (pb == NULL) {
        return -1;
    }

    CMTime pts = CMTimeMakeWithSeconds(time, 240);
    if (![self.bufferAdaptor appendPixelBuffer:pb withPresentationTime:pts]) {
        NSLog(@"[VideoRecorder] appendPixelBuffer failed. status=%ld error=%@",
              (long)self.writer.status, self.writer.error);
        return -1;
    }
    return 0;
}

- (void)releaseGPUSlots
{
    if (self.slotTextures != nil) {
        [self.slotTextures removeAllObjects];
        self.slotTextures = nil;
    }
    for (int i = 0; i < self.slotCount; i++) {
        if (self.slotCVTextures != NULL && self.slotCVTextures[i] != NULL) {
            CFRelease(self.slotCVTextures[i]);
        }
        if (self.slotBuffers != NULL && self.slotBuffers[i] != NULL) {
            CVPixelBufferRelease(self.slotBuffers[i]);
        }
    }
    if (self.slotCVTextures != NULL) { free(self.slotCVTextures); self.slotCVTextures = NULL; }
    if (self.slotBuffers != NULL) { free(self.slotBuffers); self.slotBuffers = NULL; }
    self.slotCount = 0;
}

- (void)endRecording
{
    if (!self.isRecording) {
        return;
    }

    if (!self.writer)
    {
        NSLog(@"Recording hasn't been initiated.");
        return;
    }
    
    // Frames submitted through the GPU path append asynchronously on appendQueue once
    // their blit completes. Drain that queue before marking the inputs finished,
    // otherwise the tail of the recording is silently discarded.
    if (self.appendQueue != nil) {
        dispatch_sync(self.appendQueue, ^{});
    }

    [self.videoWriterInput markAsFinished];
    [self.audioWriterInput markAsFinished];
    
#if TARGET_OS_IOS
    NSString* path = self.writer.outputURL.path;
    [self.writer finishWritingWithCompletionHandler: ^{
        if (UIVideoAtPathIsCompatibleWithSavedPhotosAlbum(path)) {
            UISaveVideoAtPathToSavedPhotosAlbum(path, nil, nil, nil);
        }
    }];
#else
    [self.writer finishWritingWithCompletionHandler: ^{}];
#endif
    self.writer = NULL;
    self.videoWriterInput = NULL;
    self.audioWriterInput = NULL;
    self.bufferAdaptor = NULL;
    self.isRecording = NO;

    [self releaseGPUSlots];

    if (self.metalTextureCache != NULL) {
        CVMetalTextureCacheFlush(self.metalTextureCache, 0);
    }
}

@end


// Diagnostics, readable from C#. Only the slot path writes these, and only from
// the main thread, so plain ints are enough.
static int g_SubmittedFrames = 0;
static int g_DroppedFrames = 0;

extern "C" {

int HoloKitVideoRecorder_StartRecording(const char* filePath, int width, int height,
                                        float audioSampleRate, int audioChannelCount) {
    return [[VideoRecorder sharedInstance] startRecording:filePath width:width height:height audioSampleRate:audioSampleRate audioChannelCount:audioChannelCount];
}

int HoloKitVideoRecorder_AppendAudioFrame(void* source, int size, double time)
{
    return [[VideoRecorder sharedInstance] appendAudioFrame:source size:size time:time];
}

int HoloKitVideoRecorder_AppendVideoFrame(void* source, int size, double time)
{
    return [[VideoRecorder sharedInstance] appendVideoFrame:source size:size time:time];
}

void HoloKitVideoRecorder_EndRecording(void)
{
    [[VideoRecorder sharedInstance] endRecording];
}

// --- Slot-based GPU path ---
// Unity copies into these textures with Graphics.CopyTexture, so no Unity
// command buffer (and therefore no IUnityInterfaces) is needed.

int HoloKitVideoRecorder_AllocateGPUSlots(int count, int width, int height)
{
    return [[VideoRecorder sharedInstance] allocateGPUSlots:count width:width height:height];
}

void* HoloKitVideoRecorder_GetGPUSlotTexture(int index)
{
    return [[VideoRecorder sharedInstance] gpuSlotTexture:index];
}

int HoloKitVideoRecorder_SubmitGPUSlot(int index, double time)
{
    int result = [[VideoRecorder sharedInstance] submitGPUSlot:index time:time];
    if (result == 0) {
        g_SubmittedFrames++;
    } else {
        g_DroppedFrames++;
    }
    return result;
}

void HoloKitVideoRecorder_ReleaseGPUSlots(void)
{
    [[VideoRecorder sharedInstance] releaseGPUSlots];
}


int HoloKitVideoRecorder_GetSubmittedFrameCount(void) { return g_SubmittedFrames; }

int HoloKitVideoRecorder_GetDroppedFrameCount(void) { return g_DroppedFrames; }

void HoloKitVideoRecorder_ResetGPUCounters(void)
{
    g_SubmittedFrames = 0;
    g_DroppedFrames = 0;
}

}
