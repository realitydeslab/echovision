using System;
using System.Collections.Generic;
using HoloKit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.UI;

public class PowerSaver : MonoBehaviour
{
    // Turning the phone fully upside down - gravity pointing up the shown picture, e.g. the headset
    // flipped over - is the gesture that puts EchoVision to sleep, so the app never rotates to
    // an upside-down orientation. Judged by the direction of gravity, not the OS orientation.
    [SerializeField]
    float upsideDownWithinDegrees = 45;

    [SerializeField]
    float secondToEnterSleepMode = 5;

    [SerializeField]
    float secondToExitSleepMode = 2;

    [SerializeField]
    List<GameObject> objectListToPutInSleepMode;

    [SerializeField]
    Image coverImage;

    public UnityEvent OnEnterSleepMode;


    bool sleepMode = false;
    public bool IsAsleep => sleepMode;
    float idleTime = 0;
    float animationDuration = 2;
    float animationTime = 0;
    float animationDirection = 0;
    int awakeFrameRate;
    bool objectsAsleep;
    const int SleepFrameRate = 5;
    Func<Vector3> readScreenGravity = ReadScreenGravity;
    

    void Update()
    {
        if (IsUpsideDown(readScreenGravity()))
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

    bool IsUpsideDown(Vector3 screenGravity) =>
        screenGravity.sqrMagnitude > .25f && Vector3.Angle(screenGravity, Vector3.up) < upsideDownWithinDegrees;

    // Gravity in screen space (x to the right of the shown picture, y up it), from CoreMotion's
    // gravity, or the accelerometer where there is none. Zero without a sensor (the Editor).
    static Vector3 ReadScreenGravity()
    {
        Vector3 gravity;
        if (GravitySensor.current is { } sensor)
        {
            if (!sensor.enabled) InputSystem.EnableDevice(sensor);
            gravity = sensor.gravity.ReadValue();
        }
        else if (Accelerometer.current is { } accelerometer)
        {
            if (!accelerometer.enabled) InputSystem.EnableDevice(accelerometer);
            gravity = accelerometer.acceleration.ReadValue();
        }
        else return Vector3.zero;
        if (InputSystem.settings.compensateForScreenOrientation) return gravity;
        return Screen.orientation switch
        {
            ScreenOrientation.LandscapeLeft => new Vector3(-gravity.y, gravity.x, gravity.z),
            ScreenOrientation.LandscapeRight => new Vector3(gravity.y, -gravity.x, gravity.z),
            ScreenOrientation.PortraitUpsideDown => new Vector3(-gravity.x, -gravity.y, gravity.z),
            _ => gravity,
        };
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
