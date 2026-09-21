using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


/// <summary>
/// 
/// Shares the camera display transform with human-stencil consumers.
/// 
/// </summary>
public class DepthImageProcessor : MonoBehaviour
{
    /// <summary>
    /// Get or set the <c>ARCameraManager</c>.
    /// </summary>
    public ARCameraManager cameraManager
    {
        get => m_CameraManager;
        set => m_CameraManager = value;
    }
    [Tooltip("The ARCameraManager which will produce camera frame events.")]
    [SerializeField] ARCameraManager m_CameraManager;

    /// <summary>
    /// Get or set the <c>AROcclusionManager</c>.
    /// </summary>
    public AROcclusionManager occlusionManager
    {
        get => m_OcclusionManager;
        set => m_OcclusionManager = value;
    }
    [Tooltip("The AROcclusionManager which will produce depth textures.")]
    [SerializeField] AROcclusionManager m_OcclusionManager;

    ARCameraBackground background;
    Matrix4x4 displayMatrix = Matrix4x4.identity;
    static readonly int DisplayTransform = Shader.PropertyToID("_UnityDisplayTransform");

    // Keep the full crop/scale and the same orientation as the camera image.
    // The iOS 27 compatibility component updates this material before rendering.
    public Matrix4x4 DisplayRotatioMatrix
    {
        get
        {
            var material = background != null ? background.material : null;
            return material != null && material.HasMatrix(DisplayTransform)
                ? material.GetMatrix(DisplayTransform) : displayMatrix;
        }
    }

    public Texture2D HumanStencilTexture
    {
        get
        {
            // ARKit may advertise support before returning a valid native texture.
            // Guard the descriptor before AR Foundation creates its texture wrapper.
            var subsystem = occlusionManager != null ? occlusionManager.subsystem : null;
            if (subsystem == null || !subsystem.running ||
                !subsystem.TryGetHumanStencil(out var descriptor) || !descriptor.valid ||
                descriptor.textureType != XRTextureType.Texture2D)
                return null;
            return occlusionManager.humanStencilTexture;
        }
    }


    void OnEnable()
    {
        Debug.Assert(m_CameraManager != null, "no camera manager");
        background = m_CameraManager.GetComponent<ARCameraBackground>();
        m_CameraManager.frameReceived += OnCameraFrameEventReceived;
        displayMatrix = Matrix4x4.identity;
    }

    void OnDisable()
    {
        m_CameraManager.frameReceived -= OnCameraFrameEventReceived;
        displayMatrix = Matrix4x4.identity;
    }

    void OnCameraFrameEventReceived(ARCameraFrameEventArgs args)
    {
        displayMatrix = args.displayMatrix ?? Matrix4x4.identity;
#if UNITY_ANDROID
        // Consumers use the ARKit row-vector shader convention.
        displayMatrix = displayMatrix.transpose;
#endif
    }
}
