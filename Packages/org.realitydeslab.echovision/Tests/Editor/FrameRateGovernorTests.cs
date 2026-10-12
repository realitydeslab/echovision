#if UNITY_IOS
using System;
using System.Reflection;
using HoloKit;
using NUnit.Framework;
using UnityEngine;

// Mono follows the thermal state as the SDK does for Stereo: 60 fps, 40 when Serious, 30 when Critical.
public class FrameRateGovernorTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Type Governor = Type.GetType("FrameRateGovernor, EchoVision", true);
    GameObject root;
    Component governor, saver;
    HoloKitCameraManager manager;
    int previousFrameRate;

    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    void Thermal(int state) => Set(governor, "readThermalState", (Func<int>)(() => state));
    void Tick() => governor.GetType().GetMethod("Govern", Private).Invoke(governor, null);

    [SetUp] public void SetUp()
    {
        previousFrameRate = Application.targetFrameRate;
        Application.targetFrameRate = 60;
        root = new GameObject("Frame rate test"); root.SetActive(false);
        manager = root.AddComponent<HoloKitCameraManager>();
        saver = root.AddComponent(Type.GetType("PowerSaver, EchoVision", true));
        governor = root.AddComponent(Governor);
        Set(governor, "cameraManager", manager);
        Set(governor, "powerSaver", saver);
    }

    [TearDown] public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        Application.targetFrameRate = previousFrameRate;
    }

    [TestCase(0, 60)] [TestCase(1, 60)] [TestCase(2, 40)] [TestCase(3, 30)]
    public void MonoFrameRate_FollowsThermalState(int thermalState, int expected)
    {
        Assert.That(Governor.GetMethod("MonoFrameRateFor").Invoke(null, new object[] { thermalState }), Is.EqualTo(expected));
    }

    [Test] public void Mono_HotPhone_LowersAndRestoresFrameRate()
    {
        Thermal(2); Tick();
        Assert.That(Application.targetFrameRate, Is.EqualTo(40));
        Thermal(0); Tick();
        Assert.That(Application.targetFrameRate, Is.EqualTo(60));
    }

    [Test] public void Stereo_IsLeftToTheSdk()
    {
        Set(manager, "m_ScreenRenderMode", ScreenRenderMode.Stereo);
        Thermal(3); Tick();
        Assert.That(Application.targetFrameRate, Is.EqualTo(60));
    }

    [Test] public void Asleep_PowerSaverKeepsItsFrameRate()
    {
        Set(saver, "sleepMode", true);
        Application.targetFrameRate = 5;
        Thermal(0); Tick();
        Assert.That(Application.targetFrameRate, Is.EqualTo(5));
    }
}
#endif
