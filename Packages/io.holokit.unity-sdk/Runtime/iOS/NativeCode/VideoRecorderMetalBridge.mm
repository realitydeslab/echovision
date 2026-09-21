// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT
//
// Render-thread bridge for the zero-copy GPU recording path.
//
// Metal commands must be encoded on Unity's render thread and into Unity's own
// command buffer, so that the blit is ordered after Unity has finished rendering
// the source texture. C# therefore cannot call the recorder directly: it enqueues
// (texture, timestamp) pairs from the main thread and raises a plugin event, which
// Unity replays on the render thread where we do the actual work.
//
// Used on both iOS and macOS (Editor), so the Editor exercises exactly the same
// code path that ships on device -- no divergence between what you test and what
// you build.

#include <TargetConditionals.h>

#if TARGET_OS_IOS || TARGET_OS_OSX

#import <Metal/Metal.h>
#import <Foundation/Foundation.h>
#import "VideoRecorder.h"

#include "IUnityInterface.h"
#include "IUnityGraphics.h"
#include "IUnityGraphicsMetal.h"

#include <deque>
#include <mutex>

namespace {

struct PendingFrame
{
    void* texturePtr;
    double timestamp;
};

IUnityInterfaces* g_UnityInterfaces = nullptr;
IUnityGraphicsMetalV1* g_MetalV1 = nullptr;
IUnityGraphicsMetalV2* g_MetalV2 = nullptr;

std::deque<PendingFrame> g_Queue;
std::mutex g_QueueMutex;

// Diagnostics, readable from C#.
int g_SubmittedFrames = 0;
int g_DroppedFrames = 0;

id<MTLCommandBuffer> CurrentCommandBuffer()
{
    if (g_MetalV2 != nullptr) {
        return g_MetalV2->CurrentCommandBuffer();
    }
    if (g_MetalV1 != nullptr) {
        return g_MetalV1->CurrentCommandBuffer();
    }
    return nil;
}

id<MTLDevice> MetalDevice()
{
    if (g_MetalV2 != nullptr) {
        return g_MetalV2->MetalDevice();
    }
    if (g_MetalV1 != nullptr) {
        return g_MetalV1->MetalDevice();
    }
    return nil;
}

void EndCurrentCommandEncoder()
{
    // A blit encoder cannot be opened while Unity has a render encoder active.
    if (g_MetalV2 != nullptr) {
        g_MetalV2->EndCurrentCommandEncoder();
    } else if (g_MetalV1 != nullptr) {
        g_MetalV1->EndCurrentCommandEncoder();
    }
}

void UNITY_INTERFACE_API OnRenderEvent(int /*eventId*/)
{
    PendingFrame frame;
    {
        std::lock_guard<std::mutex> lock(g_QueueMutex);
        if (g_Queue.empty()) {
            return;
        }
        frame = g_Queue.front();
        g_Queue.pop_front();
    }

    id<MTLTexture> srcTexture = (__bridge id<MTLTexture>)frame.texturePtr;
    if (srcTexture == nil) {
        g_DroppedFrames++;
        return;
    }

    id<MTLCommandBuffer> commandBuffer = CurrentCommandBuffer();
    if (commandBuffer == nil) {
        g_DroppedFrames++;
        return;
    }

    EndCurrentCommandEncoder();

    int status = [[VideoRecorder sharedInstance] appendVideoFrameFromMetalTexture:srcTexture
                                                                    commandBuffer:commandBuffer
                                                                             time:frame.timestamp];
    if (status == 0) {
        g_SubmittedFrames++;
    } else {
        // -3 means the writer applied backpressure. The timestamp travelled with the
        // frame, so dropping both keeps audio and video aligned.
        g_DroppedFrames++;
    }
}

} // namespace

extern "C" {

void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
    g_UnityInterfaces = unityInterfaces;
    g_MetalV2 = unityInterfaces->Get<IUnityGraphicsMetalV2>();
    g_MetalV1 = unityInterfaces->Get<IUnityGraphicsMetalV1>();

    id<MTLDevice> device = MetalDevice();
    [[VideoRecorder sharedInstance] setMetalDevice:device];

    NSLog(@"[VideoRecorderMetalBridge] loaded. metalV2=%d metalV1=%d device=%@",
          g_MetalV2 != nullptr, g_MetalV1 != nullptr, device.name);
}

void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload(void)
{
    [[VideoRecorder sharedInstance] setMetalDevice:nil];
    {
        std::lock_guard<std::mutex> lock(g_QueueMutex);
        g_Queue.clear();
    }
    g_MetalV1 = nullptr;
    g_MetalV2 = nullptr;
    g_UnityInterfaces = nullptr;
}

/// True when Unity handed us a Metal device, i.e. the GPU path is usable.
bool HoloKitVideoRecorder_IsGPUPathAvailable(void)
{
    return MetalDevice() != nil;
}

/// Called from the main thread. The timestamp travels with the frame so that a
/// dropped frame cannot desynchronise the ones after it.
void HoloKitVideoRecorder_EnqueueGPUFrame(void* texturePtr, double timestamp)
{
    if (texturePtr == nullptr) {
        return;
    }
    std::lock_guard<std::mutex> lock(g_QueueMutex);
    // Bound the queue: if the render thread falls behind, drop the oldest rather
    // than growing without limit.
    if (g_Queue.size() > 8) {
        g_Queue.pop_front();
        g_DroppedFrames++;
    }
    g_Queue.push_back({texturePtr, timestamp});
}

UnityRenderingEvent HoloKitVideoRecorder_GetRenderEventFunc(void)
{
    return OnRenderEvent;
}

int HoloKitVideoRecorder_GetSubmittedFrameCount(void) { return g_SubmittedFrames; }
int HoloKitVideoRecorder_GetDroppedFrameCount(void)   { return g_DroppedFrames; }

void HoloKitVideoRecorder_ResetGPUCounters(void)
{
    g_SubmittedFrames = 0;
    g_DroppedFrames = 0;
}

} // extern "C"

#endif // TARGET_OS_IOS || TARGET_OS_OSX
