using System.Collections;
using System.Collections.Generic;
using HoloKit;
using UnityEngine;
using UnityEngine.InputSystem.XR;

public class RippleEffectEmitter : MonoBehaviour
{
    public float rippleLifeTime = 4;
    public float rippleSpeed = 1;
    public Material matRipple;

#if UNITY_EDITOR
    public GameObject prefabEnv;
#endif

    const int MAX_RIPPLE_COUNT = 10;

    float[] rippleOriginList;
    float[] rippleDirectionList;
    float[] rippleAgeList;
    float[] rippleRangeList;
    int nextEmitIndex = 0;

    Transform tfHead;

    void Start()
    {
        rippleOriginList = new float[MAX_RIPPLE_COUNT * 3];
        rippleDirectionList = new float[MAX_RIPPLE_COUNT * 3];
        rippleAgeList = new float[MAX_RIPPLE_COUNT];
        rippleRangeList = new float[MAX_RIPPLE_COUNT];
        for (int i = 0; i < MAX_RIPPLE_COUNT; i++)
        {
            rippleAgeList[i] = 1;
            rippleRangeList[i] = 0;
        }

#if UNITY_EDITOR
        Instantiate(prefabEnv, this.transform);
#endif

        tfHead = FindObjectOfType<TrackedPoseDriver>().transform;
    }

void Update()
    {
        // Simulate ripple
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("onclicked");
            // method 1
            //EmitRipple(Vector3.zero, Vector3.forward);

            // method 2
            //EmitRipple(GetHitPosition(), Vector3.forward);

            // method 3
            EmitRipple();
        }

        // Update ripple state
        for (int i = 0; i < MAX_RIPPLE_COUNT; i++)
        {
            rippleAgeList[i] += Time.deltaTime * (1.0f / rippleLifeTime);// * Mathf.Lerp(1, 0.5f, rippleAgeList[i]);//Mathf.Max(1 - rippleAgeList[i], 0.2f);
            rippleAgeList[i] = rippleAgeList[i] >= 1 ? 1 : rippleAgeList[i];

            if(rippleAgeList[i] < 1)
            {
                rippleRangeList[i] += Time.deltaTime * rippleSpeed * Mathf.Lerp(1, 0.5f, rippleAgeList[i]);
            }
        }
        matRipple.SetFloatArray("rippleAgeList", rippleAgeList);
        matRipple.SetFloatArray("rippleRangeList", rippleRangeList);
    }

    public void EmitRipple()
    {
        EmitRipple(tfHead.position, Quaternion.Euler(tfHead.eulerAngles) * Vector3.forward);
    }

    void EmitRipple(Vector3 pos, Vector3 dir)
    {
        if (rippleAgeList[nextEmitIndex] >= 1)
        {
            rippleOriginList[nextEmitIndex * 3] = pos.x;
            rippleOriginList[nextEmitIndex * 3 + 1] = pos.y;
            rippleOriginList[nextEmitIndex * 3 + 2] = pos.z;

            rippleDirectionList[nextEmitIndex * 3] = dir.x;
            rippleDirectionList[nextEmitIndex * 3 + 1] = dir.y;
            rippleDirectionList[nextEmitIndex * 3 + 2] = dir.z;


            rippleAgeList[nextEmitIndex] = 0;
            rippleRangeList[nextEmitIndex] = 0;

            matRipple.SetFloatArray("rippleOriginList", rippleOriginList);
            matRipple.SetFloatArray("rippleDirectionList", rippleDirectionList);
            matRipple.SetFloatArray("rippleAgeList", rippleAgeList);
            matRipple.SetFloatArray("rippleRangeList", rippleRangeList);

            nextEmitIndex++;
            if (nextEmitIndex >= MAX_RIPPLE_COUNT)
            {
                nextEmitIndex = 0;
            }
        }
    }
    

    Vector3 GetHitPosition()
    {
        RaycastHit hitInfo;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hitInfo, 1000))
        {
            Debug.Log("did hit");
            return hitInfo.point;
        }

        return Vector3.zero;
    }
}
