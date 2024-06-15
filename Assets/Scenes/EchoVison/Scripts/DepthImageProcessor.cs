using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


/// <summary>
/// 
/// Code is from DisplayDepthImage of UnityEngine.XR.ARFoundation.Samples
/// To make depth image correctly cover color image in both orientation modes
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
    [SerializeField]ARCameraManager m_CameraManager;

    /// <summary>
    /// Get or set the <c>AROcclusionManager</c>.
    /// </summary>
    public AROcclusionManager occlusionManager
    {
        get => m_OcclusionManager;
        set => m_OcclusionManager = value;
    }
    [Tooltip("The AROcclusionManager which will produce depth textures.")]
    [SerializeField]AROcclusionManager m_OcclusionManager;

    /// <summary>
    /// The current screen orientation remembered so that we are only updating the raw image layout when it changes.
    /// </summary>
    ScreenOrientation m_CurrentScreenOrientation;

    /// <summary>
    /// The display rotation matrix for the shader.
    /// </summary.
    Matrix4x4 m_DisplayRotationMatrix = Matrix4x4.identity;

    public Matrix4x4 DisplayRotatioMatrix { get => m_DisplayRotationMatrix; }

    public Texture2D HumanStencilTexture { get => occlusionManager.humanStencilTexture; }


    void Awake()
    {
       
    }

    void OnEnable()
    {
        // Subscribe to the camera frame received event, and initialize the display rotation matrix.
        Debug.Assert(m_CameraManager != null, "no camera manager");
        m_CameraManager.frameReceived += OnCameraFrameEventReceived;
        m_DisplayRotationMatrix = Matrix4x4.identity;

        // When enabled, get the current screen orientation, and update the raw image UI.
        m_CurrentScreenOrientation = Screen.orientation;
    }

    void OnDisable()
    {
        // Unsubscribe to the camera frame received event, and initialize the display rotation matrix.
        Debug.Assert(m_CameraManager != null, "no camera manager");
        m_CameraManager.frameReceived -= OnCameraFrameEventReceived;
        m_DisplayRotationMatrix = Matrix4x4.identity;
    }

    /// <summary>
    /// When the camera frame event is raised, capture the display rotation matrix.
    /// </summary>
    /// <param name="cameraFrameEventArgs">The arguments when a camera frame event is raised.</param>
    void OnCameraFrameEventReceived(ARCameraFrameEventArgs cameraFrameEventArgs)
    {
        // Copy the display rotation matrix from the camera.
        Matrix4x4 cameraMatrix = cameraFrameEventArgs.displayMatrix ?? Matrix4x4.identity;

        Vector2 affineBasisX = new Vector2(1.0f, 0.0f);
        Vector2 affineBasisY = new Vector2(0.0f, 1.0f);
        Vector2 affineTranslation = new Vector2(0.0f, 0.0f);
#if UNITY_IOS
        affineBasisX = new Vector2(cameraMatrix[0, 0], cameraMatrix[1, 0]);
        affineBasisY = new Vector2(cameraMatrix[0, 1], cameraMatrix[1, 1]);
        affineTranslation = new Vector2(cameraMatrix[2, 0], cameraMatrix[2, 1]);
#endif // UNITY_IOS
#if UNITY_ANDROID
            affineBasisX = new Vector2(cameraMatrix[0, 0], cameraMatrix[0, 1]);
            affineBasisY = new Vector2(cameraMatrix[1, 0], cameraMatrix[1, 1]);
            affineTranslation = new Vector2(cameraMatrix[0, 2], cameraMatrix[1, 2]);
#endif // UNITY_ANDROID

        // The camera display matrix includes scaling and offsets to fit the aspect ratio of the device. In most
        // cases, the camera display matrix should be used directly without modification when applying depth to
        // the scene because that will line up the depth image with the camera image. However, for this demo,
        // we want to show the full depth image as a picture-in-picture, so we remove these scaling and offset
        // factors while preserving the orientation.
        affineBasisX = affineBasisX.normalized;
        affineBasisY = affineBasisY.normalized;
        m_DisplayRotationMatrix = Matrix4x4.identity;
        m_DisplayRotationMatrix[0, 0] = affineBasisX.x;
        m_DisplayRotationMatrix[0, 1] = affineBasisY.x;
        m_DisplayRotationMatrix[1, 0] = affineBasisX.y;
        m_DisplayRotationMatrix[1, 1] = affineBasisY.y;
        m_DisplayRotationMatrix[2, 0] = Mathf.Round(affineTranslation.x);
        m_DisplayRotationMatrix[2, 1] = Mathf.Round(affineTranslation.y);
    }
}
