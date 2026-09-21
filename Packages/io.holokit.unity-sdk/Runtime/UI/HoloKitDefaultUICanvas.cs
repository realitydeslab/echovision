// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS
using HoloKit.iOS;
#endif

namespace HoloKit.UI
{
    public class HoloKitDefaultUICanvas : MonoBehaviour
    {
        public Text RenderModeButtonText;

        private HoloKitCameraManager m_HoloKitCameraManager;

#if UNITY_IOS
        public Text RecordButtonText;

        private HoloKitVideoRecorder m_VideoRecorder;
#endif

        private void Start()
        {
            m_HoloKitCameraManager = FindFirstObjectByType<HoloKitCameraManager>();
            if (m_HoloKitCameraManager == null)
            {
                Debug.LogWarning($"[{nameof(HoloKitDefaultUICanvas)}] No HoloKitCameraManager in the scene; this canvas is inert.");
                return;
            }
#if UNITY_IOS
            m_VideoRecorder = m_HoloKitCameraManager.GetComponentInChildren<HoloKitVideoRecorder>();
#endif
        }

        public void SwitchRenderMode()
        {
            if (m_HoloKitCameraManager == null)
                return;

            m_HoloKitCameraManager.ScreenRenderMode = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;

            if (RenderModeButtonText != null)
                RenderModeButtonText.text = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Mono ? "Stereo" : "Mono";

#if UNITY_IOS
            // RecordButtonText is optional: a project may drive the record UI itself
            // rather than through this default canvas.
            UpdateRecordButtonText();
#endif
        }

#if UNITY_IOS
        public void ToggleRecording()
        {
            if (m_VideoRecorder == null)
                return;

            m_VideoRecorder.ToggleRecording();
            UpdateRecordButtonText();
        }

        private void UpdateRecordButtonText()
        {
            if (RecordButtonText == null || m_VideoRecorder == null)
                return;

            RecordButtonText.text = m_VideoRecorder.IsRecording ? "Stop Recording" : "Start Recording";
        }
#endif
    }
}
