#if UNITY_IOS && DEVELOPMENT_BUILD && ECHOVISION_DEVICETEST
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// A 2x2 marker, red | green over blue | yellow, at the centre of the recorded camera. The host's
// recording_checks.read_marker finds it in decoded movie frames: where the colours land tells an
// upright movie from a rotated or mirrored one, and its shape tells whether the movie is stretched.
static class RecordingMarker
{
    const float Quadrant = 120; // pixels; the marker is square at any render size

    public static void Attach(Camera camera)
    {
        var canvas = new GameObject("Recording orientation marker").AddComponent<Canvas>();
        // Screen Space - Camera: drawn by the capture camera, so it is in every movie, Mono or Stereo.
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = camera.nearClipPlane + .01f;
        canvas.sortingOrder = short.MaxValue;
        // Draw over the AR background and VFX whatever the depth buffer holds.
        var material = new Material(Canvas.GetDefaultCanvasMaterial());
        material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        var colours = new[] { Color.red, Color.green, Color.blue, Color.yellow }; // top-left, top-right, bottom-left, bottom-right
        for (int i = 0; i < 4; i++)
        {
            var image = new GameObject(colours[i].ToString()).AddComponent<Image>();
            image.material = material;
            image.color = colours[i];
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(Quadrant, Quadrant);
            rect.anchoredPosition = new Vector2(i % 2 == 0 ? -Quadrant / 2 : Quadrant / 2, i < 2 ? Quadrant / 2 : -Quadrant / 2);
        }
    }
}
#endif
