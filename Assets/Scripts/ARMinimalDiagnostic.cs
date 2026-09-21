using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
///
/// Minimal AR Foundation orientation probe.
///
/// Logs Screen.orientation and the raw displayMatrix once per second so a stock
/// AR Foundation setup can be compared against the full EchoVision scene. If this
/// scene shows the same 90 degree rotation, the fault is upstream (Unity, AR
/// Foundation or iOS) rather than in project or HoloKit SDK code.
///
/// </summary>
public class ARMinimalDiagnostic : MonoBehaviour
{
    [SerializeField] ARCameraManager m_CameraManager;

    float m_Timer;

    void OnEnable()
    {
        if (m_CameraManager == null)
        {
            m_CameraManager = FindFirstObjectByType<ARCameraManager>();
        }

        if (m_CameraManager != null)
        {
            m_CameraManager.frameReceived += OnFrameReceived;
        }
        else
        {
            Debug.LogError("[ARMin] No ARCameraManager found.");
        }
    }

    void OnDisable()
    {
        if (m_CameraManager != null)
        {
            m_CameraManager.frameReceived -= OnFrameReceived;
        }
    }

    void OnFrameReceived(ARCameraFrameEventArgs args)
    {
        m_Timer += Time.deltaTime;
        if (m_Timer < 1.0f)
        {
            return;
        }
        m_Timer = 0f;

        Matrix4x4 m = args.displayMatrix ?? Matrix4x4.identity;
        // deviceOrientation is the accelerometer's view of the physical pose,
        // Screen.orientation the interface pose. When they agree the rendered
        // background must look upright, whatever way the phone is being held --
        // which is the only orientation check that does not depend on knowing
        // how the user happens to be holding it.
        Debug.Log($"[ARMin] t={Time.realtimeSinceStartup:F1} device={Input.deviceOrientation} " +
                  $"orientation={Screen.orientation} size={Screen.width}x{Screen.height} " +
                  $"hasMatrix={args.displayMatrix.HasValue} " +
                  $"m00={m[0, 0]:F3} m01={m[0, 1]:F3} m10={m[1, 0]:F3} m11={m[1, 1]:F3} " +
                  $"m20={m[2, 0]:F3} m21={m[2, 1]:F3}");
    }
}
