#if UNITY_IOS
using System.Reflection;
using HoloKit;
using HoloKit.iOS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class RenderModeStateTests
{
    GameObject root;
    HoloKitCameraManager manager;
    HoloKitVideoRecorder recorder;
    Camera mono, left, right, black;
    ARCameraBackground background;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    void Recording(bool value) => typeof(HoloKitVideoRecorder).GetProperty("IsRecording").SetValue(recorder, value);
    Camera Camera(string name)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform);
        return go.AddComponent<Camera>();
    }

    [SetUp] public void SetUp()
    {
        root = new GameObject("State test"); root.SetActive(false);
        manager = root.AddComponent<HoloKitCameraManager>();
        mono = Camera("Mono"); left = Camera("Left"); right = Camera("Right"); black = Camera("Black");
        background = mono.gameObject.AddComponent<ARCameraBackground>();
        recorder = mono.gameObject.AddComponent<HoloKitVideoRecorder>();
        Set(manager,"m_MonoCamera",mono); Set(manager,"m_LeftEyeCamera",left);
        Set(manager,"m_RightEyeCamera",right); Set(manager,"m_BlackCamera",black);
        Set(manager,"m_CenterEyePose",new GameObject("Center").transform);
        ((Transform)manager.CenterEyePose).SetParent(root.transform);
        Set(manager,"m_ARCameraBackground",background);
        Set(manager,"m_ShowAlignmentMarkerInStereoMode",false);
        Set(recorder,"_camera",mono); Set(recorder,"_background",background);
        Set(recorder,"_cameraManager",manager);
        manager.OnScreenRenderModeChanged += mode => Call(recorder,"ApplyRenderMode",mode);
    }

    [TearDown] public void TearDown()
    {
        Recording(false);
        Object.DestroyImmediate(root);
    }

    [TestCase(false)] [TestCase(true)]
    public void MonoStereoRoundTrip_PreservesCaptureCameraAndPose(bool recording)
    {
        Recording(recording);
        mono.transform.localRotation = Quaternion.Euler(13,27,41);
        var original = mono.transform.localRotation;
        int events = 0;
        manager.OnScreenRenderModeChanged += _ => events++;
        for (int i=0;i<3;i++)
        {
            manager.ScreenRenderMode = ScreenRenderMode.Stereo;
            Assert.That(mono.enabled, Is.EqualTo(recording));
            Assert.That(background.enabled, Is.EqualTo(recording));
            Assert.That(left.gameObject.activeSelf && right.gameObject.activeSelf && black.gameObject.activeSelf, Is.True);
            manager.ScreenRenderMode = ScreenRenderMode.Mono;
            Assert.That(mono.enabled && background.enabled, Is.True);
            Assert.That(left.gameObject.activeSelf || right.gameObject.activeSelf || black.gameObject.activeSelf, Is.False);
            Assert.That(Quaternion.Angle(original,mono.transform.localRotation), Is.LessThan(.001f));
        }
        Assert.That(events, Is.EqualTo(6));
        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        Assert.That(events, Is.EqualTo(6), "setting the same mode must not retrigger side effects");
    }

    [Test] public void Stereo_RecordThenStop_RestoresDisplayCameras()
    {
        manager.ScreenRenderMode = ScreenRenderMode.Stereo;
        Recording(true); Call(recorder,"ApplyRenderMode",ScreenRenderMode.Stereo);
        Assert.That(mono.enabled && background.enabled, Is.True);
        Recording(false); Call(recorder,"ApplyRenderMode",ScreenRenderMode.Stereo);
        Assert.That(mono.enabled || background.enabled, Is.False);
        Assert.That(left.gameObject.activeSelf && right.gameObject.activeSelf, Is.True);
    }
}
#endif
