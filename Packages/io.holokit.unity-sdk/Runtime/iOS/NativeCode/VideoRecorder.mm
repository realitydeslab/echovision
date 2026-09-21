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
@property (assign) bool isRecording;
@property (assign) bool isFinishing;
@property (assign) int width;
@property (assign) int height;

// IOSurface-backed recording textures.
@property (nonatomic, strong) id<MTLDevice> currentMetalDevice;
@property (assign) CVMetalTextureCacheRef metalTextureCache;

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

- (int)startRecording:(const char *)filePath width:(int)width height:(int)height audioSampleRate:(float)audioSampleRate audioChannelCount:(int)audioChannelCount
{
    if (self.writer || self.isFinishing)
    {
        NSLog(@"Recording has already been initiated.");
        return -1;
    }
    
    // Asset writer setup
    NSURL* filePathURL = [NSURL fileURLWithPath:[NSString stringWithUTF8String:filePath]];

    if (width <= 0 || height <= 0 || audioSampleRate <= 0 ||
        (audioChannelCount != 1 && audioChannelCount != 2)) return -1;

    NSError* err = nil;
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
    audioFormat.mFormatFlags = kAudioFormatFlagsNativeFloatPacked; // Flags specific for the format
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
   
    if (![self.writer canAddInput:self.videoWriterInput] ||
        ![self.writer canAddInput:self.audioWriterInput]) {
        NSLog(@"[VideoRecorder] HEVC/AAC inputs unavailable for this writer");
        return -1;
    }
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

    if (self.writer.status == AVAssetWriterStatusFailed) return -1;
    if (!self.audioWriterInput.isReadyForMoreMediaData)
    {
        return -3; // temporary encoder backpressure
    }

    // Write _audioInputWriter with buffer

    if (size <= 0 || size % self.audioFormat.mBytesPerFrame != 0) return -1;
    CMTime presentationTimestamp = CMTimeMakeWithSeconds(time, (int32_t)self.audioFormat.mSampleRate);
    
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
    BOOL appended = [self.bufferAdaptor appendPixelBuffer:pixelBuffer
                    withPresentationTime:CMTimeMakeWithSeconds(time, 1000000000)];
    
    CVPixelBufferRelease(pixelBuffer);

    return appended ? 0 : -1;
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

    self.slotBuffers = (CVPixelBufferRef*)calloc(count, sizeof(CVPixelBufferRef));
    self.slotCVTextures = (CVMetalTextureRef*)calloc(count, sizeof(CVMetalTextureRef));
    self.slotTextures = [NSMutableArray arrayWithCapacity:count];
    for (int i = 0; i < count; i++) [self.slotTextures addObject:(id)[NSNull null]];
    self.slotCount = count;
    return count;
}

- (void*)acquireGPUSlot:(int)index
{
    if (!self.isRecording || index < 0 || index >= self.slotCount) return NULL;
    // The C# fence has passed before a slot is recycled. AVAssetWriter can
    // still retain its previous buffer; the pool will not return that buffer.
    self.slotTextures[index] = (id)[NSNull null];
    if (self.slotCVTextures[index]) CFRelease(self.slotCVTextures[index]);
    if (self.slotBuffers[index]) CVPixelBufferRelease(self.slotBuffers[index]);
    self.slotCVTextures[index] = NULL;
    self.slotBuffers[index] = NULL;
    NSDictionary* limits = @{(NSString*)kCVPixelBufferPoolAllocationThresholdKey: @8};
    CVPixelBufferRef pb = NULL;
    if (CVPixelBufferPoolCreatePixelBufferWithAuxAttributes(kCFAllocatorDefault,
            self.bufferAdaptor.pixelBufferPool, (__bridge CFDictionaryRef)limits, &pb) != kCVReturnSuccess)
        return NULL;
    CVMetalTextureRef cvTex = NULL;
    CVReturn result = CVMetalTextureCacheCreateTextureFromImage(kCFAllocatorDefault,
        self.metalTextureCache, pb, NULL, MTLPixelFormatBGRA8Unorm,
        self.width, self.height, 0, &cvTex);
    if (result != kCVReturnSuccess || !cvTex) { CVPixelBufferRelease(pb); return NULL; }
    id<MTLTexture> texture = CVMetalTextureGetTexture(cvTex);
    if (!texture) { CFRelease(cvTex); CVPixelBufferRelease(pb); return NULL; }
    self.slotBuffers[index] = pb;
    self.slotCVTextures[index] = cvTex;
    self.slotTextures[index] = texture;
    return (__bridge void*)texture;
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

    CMTime pts = CMTimeMakeWithSeconds(time, 1000000000);
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

- (void)resetWriter
{
    self.isRecording = NO;
    self.writer = nil;
    self.videoWriterInput = nil;
    self.audioWriterInput = nil;
    self.bufferAdaptor = nil;
    if (self.cmFormat) { CFRelease(self.cmFormat); self.cmFormat = NULL; }
}

- (void)endRecording
{
    if (!self.isRecording) return;
    self.isRecording = NO;
    AVAssetWriter* writer = self.writer;
    if (writer.status == AVAssetWriterStatusWriting) {
        [self.videoWriterInput markAsFinished];
        [self.audioWriterInput markAsFinished];
        self.isFinishing = YES;
        [writer finishWritingWithCompletionHandler:^{
            if (writer.status == AVAssetWriterStatusCompleted) {
                NSLog(@"[VideoRecorder] SAVED %@", writer.outputURL.path);
#if TARGET_OS_IOS
                dispatch_async(dispatch_get_main_queue(), ^{
                    if (UIVideoAtPathIsCompatibleWithSavedPhotosAlbum(writer.outputURL.path))
                        UISaveVideoAtPathToSavedPhotosAlbum(writer.outputURL.path, nil, nil, nil);
                });
#endif
            } else NSLog(@"[VideoRecorder] FINALIZE FAILED %@", writer.error);
            @synchronized(self) { self.isFinishing = NO; }
        }];
    } else {
        NSLog(@"[VideoRecorder] RECORDING FAILED %@", writer.error);
        [writer cancelWriting];
    }
    [self resetWriter];
}

@end


// Serialize native lifecycle and appends across Unity's main/audio threads.
extern "C" {
int HoloKitVideoRecorder_StartRecording(const char* path, int width, int height, float rate, int channels) {
    VideoRecorder* recorder = [VideoRecorder sharedInstance];
    @synchronized(recorder) {
        if (recorder.isRecording || recorder.isFinishing) return -2;
        int result = [recorder startRecording:path width:width height:height audioSampleRate:rate audioChannelCount:channels];
        if (result != 0) { [recorder.writer cancelWriting]; [recorder resetWriter]; }
        return result;
    }
}
int HoloKitVideoRecorder_AppendAudioFrame(void* data, int size, double time) {
    VideoRecorder* recorder = [VideoRecorder sharedInstance];
    @synchronized(recorder) { return [recorder appendAudioFrame:data size:size time:time]; }
}
int HoloKitVideoRecorder_AppendVideoFrame(void* data, int size, double time) {
    VideoRecorder* recorder = [VideoRecorder sharedInstance];
    @synchronized(recorder) { return [recorder appendVideoFrame:data size:size time:time]; }
}
void HoloKitVideoRecorder_EndRecording() {
    VideoRecorder* recorder = [VideoRecorder sharedInstance];
    @synchronized(recorder) { [recorder endRecording]; }
}
int HoloKitVideoRecorder_IsFinishing() {
    VideoRecorder* recorder = [VideoRecorder sharedInstance];
    @synchronized(recorder) { return recorder.isFinishing ? 1 : 0; }
}
int HoloKitVideoRecorder_AllocateGPUSlots(int count, int width, int height) {
    return [[VideoRecorder sharedInstance] allocateGPUSlots:count width:width height:height];
}
void* HoloKitVideoRecorder_AcquireGPUSlot(int index) {
    return [[VideoRecorder sharedInstance] acquireGPUSlot:index];
}
int HoloKitVideoRecorder_SubmitGPUSlot(int index, double time) {
    VideoRecorder* recorder = [VideoRecorder sharedInstance];
    @synchronized(recorder) { return [recorder submitGPUSlot:index time:time]; }
}
void HoloKitVideoRecorder_ReleaseGPUSlots() { [[VideoRecorder sharedInstance] releaseGPUSlots]; }
}
