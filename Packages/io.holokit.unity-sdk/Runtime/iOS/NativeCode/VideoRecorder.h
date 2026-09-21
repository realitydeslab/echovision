// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <botao.a.hu@gmail.com>
// SPDX-License-Identifier: MIT

#import <Foundation/Foundation.h>
#import <Metal/Metal.h>

@interface VideoRecorder : NSObject

+ (id)sharedInstance;

/// Metal device used to build the CVMetalTextureCache for the zero-copy GPU path.
/// MUST be Unity's own device, otherwise the wrapped textures cannot be shared.
/// Call before startRecording; passing nil disables the GPU path.
- (void)setMetalDevice:(id<MTLDevice>)device;

/// Zero-copy frame submission. Blits `srcTexture` straight into an IOSurface-backed
/// CVPixelBuffer on the GPU, so pixels never touch the CPU.
///
/// `commandBuffer` must be Unity's current command buffer, so the blit is ordered
/// after Unity has finished rendering into the source texture. Must be called on
/// the render thread.
///
/// Returns 0 on success, -3 when the frame was intentionally dropped (writer not
/// ready — caller should put the timestamp back), negative otherwise.
- (int)appendVideoFrameFromMetalTexture:(id<MTLTexture>)srcTexture
                          commandBuffer:(id<MTLCommandBuffer>)commandBuffer
                                   time:(double)time;

@end
