using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation.Samples;

public class GameManager : MonoBehaviour
{
    private Transform headTransform;
    public Transform HeadTransform { get { return headTransform; } }

    private ARMeshManager meshManager;
    public ARMeshManager MeshManager { get { return meshManager; } }

    private AROcclusionManager occlusionManager;
    public AROcclusionManager OcclusionManager { get { return occlusionManager; } }

    private DisplayDepthImage depthImageProcessor;
    public DisplayDepthImage DepthImageProcessor { get { return depthImageProcessor; } }

    private AudioProcessor audioProcessor;
    private float audioVolume;
    public float AudioVolume { get { return audioProcessor.AudioVolume; } }
    public float AudioPitch { get { return audioProcessor.AudioPitch; } }

    private Helper helper;
    public Helper Helper { get { return helper; } }

    void Start()
    {
        headTransform = FindObjectOfType<TrackedPoseDriver>().transform;
        if(headTransform == null)
        {
            Debug.LogError("No TrackedPoseDriver Found.");
        }

        meshManager = FindObjectOfType<ARMeshManager>();
        if (meshManager == null)
        {
            Debug.LogError("No ARMeshManager Found.");
        }

        occlusionManager = FindObjectOfType<AROcclusionManager>();
        if (occlusionManager == null)
        {
            Debug.LogError("No AROcclusionManager Found.");
        }

        depthImageProcessor = FindObjectOfType<DisplayDepthImage>();
        if (depthImageProcessor == null)
        {
            Debug.LogError("No DisplayDepthImage Found.");
        }

        audioProcessor = FindObjectOfType<AudioProcessor>();
        if (audioProcessor == null)
        {
            Debug.LogError("No AudioProcessor Found.");
        }

        helper = FindObjectOfType<Helper>();
        if (helper == null)
        {
            Debug.LogError("No Healper Found.");
        }
    }


    public void SetInfo(string name, string text)
    {
        helper?.SetInfo(name, text);
    }

    public void SetLabel(string name, Vector3 pos, string text)
    {
        helper?.SetLabel(name, pos, text);
    }
    public void SetHumanTexture(Texture2D tex)
    {
        helper?.SetHumanTexture(tex);
    }

    #region Instance
    private static GameManager _Instance;

    public static GameManager Instance
    {
        get
        {
            if (_Instance == null)
            {
                _Instance = GameObject.FindObjectOfType<GameManager>();
                if (_Instance == null)
                {
                    GameObject go = new GameObject();
                    _Instance = go.AddComponent<GameManager>();
                }
            }
            return _Instance;
        }
    }
    private void Awake()
    {
        if (_Instance != null)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        _Instance = null;
    }
    #endregion
}
