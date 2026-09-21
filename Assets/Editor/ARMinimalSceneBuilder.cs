using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;

/// <summary>
///
/// Builds a stock AR Foundation scene with nothing else in it, so the iOS 27
/// background rotation can be reproduced (or ruled out) without HoloKit SDK or
/// any EchoVision code in the picture.
///
/// Usage:
///   unity run . -- -executeMethod ARMinimalSceneBuilder.CreateScene
///   unity run . -- -buildTarget iOS -executeMethod ARMinimalSceneBuilder.BuildiOS
///
/// </summary>
public static class ARMinimalSceneBuilder
{
    const string k_ScenePath = "Assets/Scenes/ARMinimal.unity";
    const string k_BuildOutput = "Build_ARMinimal";

    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // AR Session
        var sessionGo = new GameObject("AR Session");
        sessionGo.AddComponent<ARSession>();
        sessionGo.AddComponent<ARInputManager>();

        // XR Origin -> Camera Offset -> AR Camera
        var originGo = new GameObject("XR Origin");
        var origin = originGo.AddComponent<XROrigin>();

        var offsetGo = new GameObject("Camera Offset");
        offsetGo.transform.SetParent(originGo.transform, false);

        var cameraGo = new GameObject("AR Camera");
        cameraGo.transform.SetParent(offsetGo.transform, false);
        cameraGo.tag = "MainCamera";

        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 20f;

        cameraGo.AddComponent<TrackedPoseDriver>();
        var cameraManager = cameraGo.AddComponent<ARCameraManager>();
        cameraGo.AddComponent<ARCameraBackground>();

        origin.Camera = camera;
        origin.CameraFloorOffsetObject = offsetGo;

        // Diagnostic probe
        var probeGo = new GameObject("AR Minimal Diagnostic");
        var probe = probeGo.AddComponent<ARMinimalDiagnostic>();
        var so = new SerializedObject(probe);
        so.FindProperty("m_CameraManager").objectReferenceValue = cameraManager;
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(k_ScenePath));
        EditorSceneManager.SaveScene(scene, k_ScenePath);

        Debug.Log($"[ARMinimalSceneBuilder] Created {k_ScenePath}");
        EditorApplication.Exit(0);
    }

    public static void BuildiOS()
    {
        if (!File.Exists(k_ScenePath))
        {
            Debug.LogError($"[ARMinimalSceneBuilder] Missing {k_ScenePath}; run CreateScene first.");
            EditorApplication.Exit(1);
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = new[] { k_ScenePath },
            locationPathName = Path.Combine(Directory.GetCurrentDirectory(), k_BuildOutput),
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[ARMinimalSceneBuilder] Build {report.summary.result}, " +
                           $"{report.summary.totalErrors} error(s)");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[ARMinimalSceneBuilder] Build succeeded -> {report.summary.outputPath}");
        EditorApplication.Exit(0);
    }
}
