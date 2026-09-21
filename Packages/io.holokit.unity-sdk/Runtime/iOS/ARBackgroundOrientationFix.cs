// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloKit.iOS
{
    /// <summary>
    ///
    /// Works around the iOS 27 AR camera background rotation bug by driving
    /// ARCameraBackground with a custom material and a display transform computed
    /// here instead of the one AR Foundation supplies.
    ///
    /// On iOS 27 AR Foundation's ARKit provider returns a wrong displayMatrix in
    /// landscape: it stays anti-diagonal where ARKit's own displayTransform goes
    /// diagonal, and it returns bit-identical matrices for LandscapeLeft and
    /// LandscapeRight even though they are 180 degrees apart. ARKit itself is
    /// correct -- a pure Swift app rotates properly on the same device -- so the
    /// fault is in the conversion, and every AR Foundation version tested (6.1.0
    /// and 6.4.3) is affected. See plan/ios27-ar-rotation.md.
    ///
    /// The transform is rebuilt from first principles out of the screen
    /// orientation, the camera texture size and the viewport size. The portrait
    /// case reproduces AR Foundation's own portrait matrix exactly, which is the
    /// check that the convention here is right; the landscape cases are that same
    /// basis rotated.
    ///
    /// TEMPORARY: delete this, Runtime/iOS/Shaders/ARKitBackgroundOrientationFix.shader
    /// and Runtime/Resources/ARKitBackgroundOrientationFix.mat once Unity ships a
    /// fix, otherwise the background will be over-rotated.
    ///
    /// </summary>
    [DisallowMultipleComponent]
    public class ARBackgroundOrientationFix : MonoBehaviour
    {
        const string k_MaterialResource = "ARKitBackgroundOrientationFix";

        static readonly int k_DisplayTransformId = Shader.PropertyToID("_EchoDisplayTransform");
        static readonly int k_DisplayTransformValidId = Shader.PropertyToID("_EchoDisplayTransformValid");
        static readonly int k_TextureYId = Shader.PropertyToID("_textureY");

        /// <summary>
        /// Installs itself rather than living in the scene, so that the workaround
        /// covers every scene and can be removed by deleting one file.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            // Only the iOS player is affected. XR Simulation in the Editor uses a
            // different background shader, so forcing this material there would
            // break the Editor view.
            if (Application.platform != RuntimePlatform.IPhonePlayer)
            {
                return;
            }

            var go = new GameObject(nameof(ARBackgroundOrientationFix));
            DontDestroyOnLoad(go);
            go.AddComponent<ARBackgroundOrientationFix>();
        }

        ARCameraBackground m_Background;
        ARCameraManager m_CameraManager;
        Material m_Material;
        ScreenOrientation m_LoggedOrientation = ScreenOrientation.AutoRotation;

        IEnumerator Start()
        {
            m_Material = Resources.Load<Material>(k_MaterialResource);
            if (m_Material == null)
            {
                Debug.LogError($"[ARBgFix] Resources/{k_MaterialResource}.mat not found; leaving the stock background in place.");
                yield break;
            }

            // The AR rig is spawned by the scene, so wait for it rather than assume
            // it already exists.
            while (m_Background == null)
            {
                m_Background = FindFirstObjectByType<ARCameraBackground>();
                if (m_Background == null)
                {
                    yield return null;
                }
            }

            m_CameraManager = m_Background.GetComponent<ARCameraManager>();
            if (m_CameraManager == null)
            {
                Debug.LogError("[ARBgFix] No ARCameraManager beside ARCameraBackground; cannot read the camera texture size.");
                yield break;
            }

            m_Background.useCustomMaterial = true;
            m_Background.customMaterial = m_Material;
            m_CameraManager.frameReceived += OnCameraFrameReceived;

            Debug.Log("[ARBgFix] Custom AR background material installed.");
        }

        void OnDestroy()
        {
            if (m_CameraManager != null)
            {
                m_CameraManager.frameReceived -= OnCameraFrameReceived;
            }

            // Hand the stock path back, so disabling this component really does
            // restore the original behaviour.
            if (m_Background != null)
            {
                m_Background.useCustomMaterial = false;
                m_Background.customMaterial = null;
            }
        }

        void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
        {
            if (!TryGetCameraTextureSize(eventArgs, out int texWidth, out int texHeight))
            {
                // Without the texture size the aspect crop cannot be computed, so
                // fall back to AR Foundation's matrix rather than guess.
                m_Material.SetFloat(k_DisplayTransformValidId, 0f);
                return;
            }

            var orientation = Screen.orientation;
            var transform = BuildDisplayTransform(orientation, texWidth, texHeight, Screen.width, Screen.height);

            m_Material.SetMatrix(k_DisplayTransformId, transform);
            m_Material.SetFloat(k_DisplayTransformValidId, 1f);

            if (orientation != m_LoggedOrientation)
            {
                m_LoggedOrientation = orientation;
                var arf = eventArgs.displayMatrix ?? Matrix4x4.zero;
                Debug.Log($"[ARBgFix] {orientation} screen={Screen.width}x{Screen.height} tex={texWidth}x{texHeight}\n" +
                          $"  ours: a={transform[0, 0]:F3} b={transform[1, 0]:F3} tx={transform[2, 0]:F3} | " +
                          $"c={transform[0, 1]:F3} d={transform[1, 1]:F3} ty={transform[2, 1]:F3}\n" +
                          $"  arf : a={arf[0, 0]:F3} b={arf[1, 0]:F3} tx={arf[2, 0]:F3} | " +
                          $"c={arf[0, 1]:F3} d={arf[1, 1]:F3} ty={arf[2, 1]:F3}");
            }
        }

        static bool TryGetCameraTextureSize(ARCameraFrameEventArgs eventArgs, out int width, out int height)
        {
            // Match on the property name rather than assume index 0 is the luma plane.
            for (int i = 0; i < eventArgs.textures.Count; i++)
            {
                if (eventArgs.propertyNameIds[i] != k_TextureYId)
                {
                    continue;
                }
                var texture = eventArgs.textures[i];
                if (texture == null)
                {
                    break;
                }
                width = texture.width;
                height = texture.height;
                return width > 0 && height > 0;
            }

            width = 0;
            height = 0;
            return false;
        }

        /// <summary>
        /// Builds the screen-UV to texture-UV transform, in the same row-vector
        /// convention the stock shader uses for _UnityDisplayTransform:
        /// texX = a*u + b*v + tx, texY = c*u + d*v + ty.
        /// </summary>
        static Matrix4x4 BuildDisplayTransform(
            ScreenOrientation orientation, int texWidth, int texHeight, int viewWidth, int viewHeight)
        {
            // Unrotated basis, before the aspect crop. The camera texture is always
            // the sensor's landscape frame; only the mapping from screen to sensor
            // axes changes with the device orientation.
            //
            // Portrait is AR Foundation's own (correct) portrait matrix with the
            // scale divided out: texture +U runs down the screen and texture +V runs
            // left. Rotating the device moves those two axes together. The
            // determinant is -1 throughout because the texture's V axis is opposite
            // to Unity's.
            float a0, b0, c0, d0;
            switch (orientation)
            {
                case ScreenOrientation.LandscapeLeft:
                    a0 = 1f; b0 = 0f; c0 = 0f; d0 = -1f;
                    break;
                case ScreenOrientation.LandscapeRight:
                    a0 = -1f; b0 = 0f; c0 = 0f; d0 = 1f;
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                    a0 = 0f; b0 = 1f; c0 = 1f; d0 = 0f;
                    break;
                default: // Portrait
                    a0 = 0f; b0 = -1f; c0 = -1f; d0 = 0f;
                    break;
            }

            // Aspect fill: cover the viewport and crop the overhang, never letterbox.
            bool quarterTurned = Mathf.Approximately(a0, 0f);
            float sensorAspect = (float)texWidth / texHeight;
            float imageAspect = quarterTurned ? 1f / sensorAspect : sensorAspect;
            float viewAspect = (float)viewWidth / viewHeight;

            float su, sv;
            if (viewAspect >= imageAspect)
            {
                // Viewport is relatively wider: use the full width, crop top and bottom.
                su = 1f;
                sv = imageAspect / viewAspect;
            }
            else
            {
                su = viewAspect / imageAspect;
                sv = 1f;
            }

            float a = a0 * su, b = b0 * sv;
            float c = c0 * su, d = d0 * sv;

            // Keep the cropped window centred on the texture.
            float tx = 0.5f - 0.5f * (a + b);
            float ty = 0.5f - 0.5f * (c + d);

            var m = Matrix4x4.identity;
            m[0, 0] = a; m[1, 0] = b; m[2, 0] = tx; m[3, 0] = 0f;
            m[0, 1] = c; m[1, 1] = d; m[2, 1] = ty; m[3, 1] = 0f;
            return m;
        }
    }
}
#endif
