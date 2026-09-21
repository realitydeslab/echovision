using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Optional development-only alignment probe: draws the LiDAR mesh
/// permanently and logs the pose, so whether the AR layer and the camera
/// background agree with each other can be judged from one still frame. Changes
/// nothing about orientation.
/// </summary>
public class ARAlignmentDebugOverlay : MonoBehaviour
{
#if UNITY_IOS && DEVELOPMENT_BUILD && ECHOVISION_DEVICETEST
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Inject()
    {
        var go = new GameObject(nameof(ARAlignmentDebugOverlay));
        DontDestroyOnLoad(go);
        go.AddComponent<ARAlignmentDebugOverlay>();
    }

    Material m_Debug;
    ARMeshManager m_MeshManager;

    IEnumerator Start()
    {
        m_Debug = Resources.Load<Material>("ARAlignmentDebug");
        if (m_Debug == null)
        {
            Debug.LogError("[ALIGN] Resources/ARAlignmentDebug.mat missing");
            yield break;
        }

        while (true)
        {
            if (m_MeshManager == null)
            {
                m_MeshManager = FindFirstObjectByType<ARMeshManager>();
            }

            int n = 0;
            if (m_MeshManager != null)
            {
                foreach (var mf in m_MeshManager.meshes)
                {
                    if (mf == null) continue;
                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mr == null) continue;
                    mr.enabled = true;
                    if (mr.sharedMaterial != m_Debug) mr.sharedMaterial = m_Debug;
                    n++;
                }
            }

            Debug.Log($"[POSE] interface={Screen.orientation} accel={Input.deviceOrientation} " +
                      $"size={Screen.width}x{Screen.height} meshes={n}");
            yield return new WaitForSeconds(1f);
        }
    }
#endif
}
