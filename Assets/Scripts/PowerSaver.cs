using System.Collections.Generic;
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

        // enable object list before aniamtion
        SetObjectListState(true);
    }

    void SetObjectListState(bool state)
    {
        foreach(var go in objectListToPutInSleepMode)
        {
            go.SetActive(state);
        }
    }
}
