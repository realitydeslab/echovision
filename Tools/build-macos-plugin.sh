#!/bin/bash
# Rebuild the macOS UnityHoloKit native plugin.
#
# Why this is needed: on non-iOS platforms HoloKitVideoRecorder.cs binds to
# DllName = "UnityHoloKit". Without this bundle, StartRecording throws
# DllNotFoundException in the Editor, IsRecording never becomes true, recording
# cannot run at all -- and the microphone feeds back into itself because the
# audio buffer is only cleared while recording.
#
# The sources are shared verbatim with iOS. The iOS-only parts (UIKit, saving to
# the photo library) are guarded by #if TARGET_OS_IOS and are skipped on macOS,
# so a macOS recording stays in Application.temporaryCachePath instead of going
# to the photo library.
#
# VideoRecorderMetalBridge.mm is included, so the Editor runs exactly the same
# zero-copy GPU path that ships on device, rather than an Editor-only code path.
#
# Usage: ./Tools/build-macos-plugin.sh
set -euo pipefail

cd "$(dirname "$0")/.."
NATIVE="Packages/io.holokit.unity-sdk/Runtime/iOS/NativeCode"
SRC="$NATIVE/VideoRecorder.mm $NATIVE/VideoRecorderMetalBridge.mm"
OUT="Assets/Plugins/macOS/UnityHoloKit.bundle"
SDK="$(xcrun --sdk macosx --show-sdk-path)"

# The bridge includes Unity's native plugin headers (IUnityGraphicsMetal.h etc).
# Resolve them from the Editor that this project is pinned to.
EDITOR_VERSION="$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt | tr -d '\r')"
UNITY_PLUGIN_API="/Applications/Unity/$EDITOR_VERSION/Unity.app/Contents/PluginAPI"
if [ ! -d "$UNITY_PLUGIN_API" ]; then
  echo "ERROR: Unity PluginAPI headers not found at $UNITY_PLUGIN_API" >&2
  echo "       (editor version read from ProjectSettings/ProjectVersion.txt)" >&2
  exit 1
fi
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

mkdir -p "$OUT/Contents/MacOS"

for ARCH in arm64 x86_64; do
  clang++ -bundle $SRC -o "$TMP/UnityHoloKit_$ARCH" \
    -fobjc-arc -std=c++17 -arch "$ARCH" -mmacosx-version-min=12.0 -isysroot "$SDK" \
    -I "$UNITY_PLUGIN_API" -I "$NATIVE" \
    -framework Foundation -framework AVFoundation -framework CoreMedia \
    -framework CoreVideo -framework Accelerate -framework AudioToolbox -framework Metal
  echo "  built $ARCH"
done

lipo -create "$TMP/UnityHoloKit_arm64" "$TMP/UnityHoloKit_x86_64" \
     -output "$OUT/Contents/MacOS/UnityHoloKit"

echo "arch: $(lipo -info "$OUT/Contents/MacOS/UnityHoloKit" | sed 's/.*are: //')"
echo "exported symbols:"
nm -gU "$OUT/Contents/MacOS/UnityHoloKit" | grep -E "HoloKitVideoRecorder|UnityPlugin" | sed 's/^/  /'
echo "OK -> $OUT"
