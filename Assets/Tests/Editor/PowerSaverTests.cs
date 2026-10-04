using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PowerSaverTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    GameObject root, workload;
    Component saver;
    int previousFrameRate;
    void Set(string field, object value) => saver.GetType().GetField(field, Private).SetValue(saver, value);
    void Call(string method) => saver.GetType().GetMethod(method, Private).Invoke(saver, null);

    [SetUp] public void SetUp()
    {
        previousFrameRate = Application.targetFrameRate;
        Application.targetFrameRate = 60;
        root = new GameObject("Sleep test"); root.SetActive(false);
        saver = root.AddComponent(Type.GetType("PowerSaver, Assembly-CSharp", true));
        workload = new GameObject("AR workload");
        var cover = new GameObject("Cover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cover.transform.SetParent(root.transform);
        Set("coverImage", cover.GetComponent<Image>());
        Set("objectListToPutInSleepMode", new List<GameObject> { workload });
    }

    [TearDown] public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(workload);
        Application.targetFrameRate = previousFrameRate;
    }

    [Test] public void Sleep_StopsRecordingBeforeWorkload_AndWakeRestoresFrameRate()
    {
        int stopped = 0;
        var stop = new UnityEvent();
        stop.AddListener(() => { Assert.That(workload.activeSelf, Is.True); stopped++; });
        saver.GetType().GetField("OnEnterSleepMode").SetValue(saver, stop);
        Call("EnterSleepMode"); Call("EnterSleepMode");
        Assert.That(stopped, Is.EqualTo(1));
        Set("animationTime", 2.01f); Call("UpdateAnimation");
        Assert.That(workload.activeSelf, Is.False);
        Assert.That(Application.targetFrameRate, Is.EqualTo(5));
        var cover = root.GetComponentInChildren<Image>(true);
        Assert.That(cover.raycastTarget, Is.True, "cover blocks taps while asleep");
        Call("ExitSleepMode");
        Assert.That(workload.activeSelf, Is.True);
        Assert.That(Application.targetFrameRate, Is.EqualTo(60));
        Assert.That(cover.raycastTarget, Is.False, "cover lets taps through when awake");
    }

    [Test] public void WakeDuringFade_PreservesAwakeFrameRate()
    {
        Call("EnterSleepMode"); Call("ExitSleepMode");
        Assert.That(workload.activeSelf, Is.True);
        Assert.That(Application.targetFrameRate, Is.EqualTo(60));
    }

    [Test] public void Asleep_UndoesExternalFrameRateChange()
    {
        Call("EnterSleepMode");
        Set("animationTime", 2.01f); Call("UpdateAnimation");
        // As HoloKitCameraManager does in Stereo when the thermal state changes.
        Application.targetFrameRate = 40;
        Set("idleOrientation", Input.deviceOrientation); Set("idleTime", 31f);
        Call("Update");
        Assert.That(Application.targetFrameRate, Is.EqualTo(5));
        Call("ExitSleepMode");
        Assert.That(Application.targetFrameRate, Is.EqualTo(60));
    }

    [TestCase(29f, false)]
    [TestCase(30.01f, true)]
    public void RestOrientation_UsesThirtySecondThreshold(float elapsed, bool expected)
    {
        Set("idleOrientation", Input.deviceOrientation); Set("idleTime", elapsed);
        Call("Update");
        Assert.That(saver.GetType().GetField("sleepMode", Private).GetValue(saver), Is.EqualTo(expected));
    }
}
