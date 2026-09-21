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

#pragma mark - Slot-based GPU path (no IUnityInterfaces required)

/// Allocates `count` IOSurface-backed CVPixelBuffers, each wrapped as an
/// MTLTexture, and returns how many were created.
///
/// This path exists because UnityPluginLoad is never invoked on iOS, where the
/// plugin is statically linked -- so IUnityGraphicsMetal, and with it Unity's
/// command buffer, is unreachable. Instead Unity copies into these textures
/// itself via Graphics.CopyTexture, which means Unity also schedules the work
/// and we never need its command buffer.
///
/// iOS has a single GPU, so MTLCreateSystemDefaultDevice() returns the very
/// device Unity is rendering with; textures are therefore shareable.
- (int)allocateGPUSlots:(int)count width:(int)width height:(int)height;

/// The MTLTexture backing slot `index`, as a pointer for
/// Texture2D.CreateExternalTexture. Returns NULL if the slot does not exist.
- (void*)gpuSlotTexture:(int)index;

/// Appends slot `index` to the writer. The caller must leave at least one frame
/// between the Graphics.CopyTexture into this slot and this call, so the GPU has
/// finished writing. Returns 0 on success, -3 if the writer applied backpressure.
- (int)submitGPUSlot:(int)index time:(double)time;

/// Releases all slots. Safe to call when none are allocated.
- (void)releaseGPUSlots;

@end
