using System.Collections.Generic;
using HoloKit;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PowerSaver : MonoBehaviour
{
    [SerializeField]
    DeviceOrientation idleOrientation = DeviceOrientation.LandscapeRight;

    [SerializeField]
    float secondToEnterSleepMode = 30;

    [SerializeField]
    float secondToExitSleepMode = 2;

    [SerializeField]
    List<GameObject> objectListToPutInSleepMode;

    [SerializeField]
    Image coverImage;

    public UnityEvent OnEnterSleepMode;


    bool sleepMode = false;
    float idleTime = 0;
    float animationDuration = 2;
    float animationTime = 0;
    float animationDirection = 0;
    int awakeFrameRate;
    bool objectsAsleep;
    const int SleepFrameRate = 5;
    

    void Update()
    {
        if (Input.deviceOrientation == idleOrientation)
        {
            idleTime += Time.deltaTime;
            if(idleTime > secondToEnterSleepMode)
            {
                idleTime = secondToEnterSleepMode;

                // enter sleep mode
                EnterSleepMode();
            }
        }
        else
        {
            idleTime = Mathf.Min(idleTime, secondToExitSleepMode);

            idleTime -= Time.deltaTime;
            if(idleTime < 0)
            {
                idleTime = 0;

                // exit sleep mode
                ExitSleepMode();
            }
        }

        UpdateAnimation();

        // HoloKitCameraManager re-applies the Stereo frame rate when the thermal state changes,
        // which happens as the phone cools down during sleep.
        if (objectsAsleep && Application.targetFrameRate != SleepFrameRate)
            Application.targetFrameRate = SleepFrameRate;
    }

    void UpdateAnimation()
    {
        animationTime += animationDirection * Time.deltaTime;
        if(animationTime > animationDuration)
        {
            animationTime = animationDuration;
            animationDirection = 0;

            // finish animaiton
            // disable object list after animation
            SetObjectListState(false);
        }
        else if(animationTime < 0)
        {
            animationTime = 0;
            animationDirection = 0;

            // finish animaiton
        }

        animationTime = Mathf.Clamp(animationTime, 0, animationDuration);

        coverImage.color = new Color(0, 0, 0, animationTime / animationDuration);
    }

    void EnterSleepMode()
    {
        if (sleepMode == true)
            return;

        sleepMode = true;
        awakeFrameRate = Application.targetFrameRate;
        animationDirection = 1;

        // if is recording, stop recording
        OnEnterSleepMode?.Invoke();
    }

    void ExitSleepMode()
    {
        if (sleepMode == false)
            return;

        sleepMode = false;
        animationDirection = -1;

        // Restore rendering before resuming AR and fading the cover away.
        Application.targetFrameRate = awakeFrameRate;
        SetObjectListState(true);

        // The thermal state may have changed while asleep. The setter re-applies the Stereo
        // frame rate for the current state; it does nothing in Mono.
        var cameraManager = FindFirstObjectByType<HoloKitCameraManager>();
        if (cameraManager != null)
            cameraManager.AdaptStereoFrameRateToThermalState = cameraManager.AdaptStereoFrameRateToThermalState;
    }

    void SetObjectListState(bool state)
    {
        foreach(var go in objectListToPutInSleepMode)
        {
            go.SetActive(state);
        }
        objectsAsleep = !state;
        // Taps on the black cover must not reach the buttons below it, and the stereo bar
        // draws above the cover, so it is hidden while asleep.
        coverImage.raycastTarget = !state;
        var cameraManager = FindFirstObjectByType<HoloKitCameraManager>();
        if (cameraManager != null && cameraManager.StereoBar != null)
            cameraManager.StereoBar.gameObject.SetActive(state && cameraManager.ScreenRenderMode == ScreenRenderMode.Stereo);
        // The ARSession in this list may restore its cached frame rate on disable.
        if (!state) Application.targetFrameRate = SleepFrameRate;
    }
}
