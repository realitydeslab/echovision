using UnityEngine;
using HoloKit;

[ExecuteInEditMode]
public class BatVFXShaderController : MonoBehaviour
{

    public Material material;

    // Initalize Array
    // This should have the same length as in the shader!
    float[] points = new float[] {
        1, 0, 0, 0f,
        0, 1, 0, 0f,
        0, 0, 1, 0f,
        -1, 0, 0, 0f,
        0, -1, 0, 0f,
        0, 0, -1, 0f,
        0, 0, -1, 0f,
        0, 0, -1, 0f,
        0, 0, -1, 0f,
    };

    bool[] activeState = new bool[]
    {
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
    };

    float scannedRemain = 0;
    float lasetTriggerTime = -.5f;


    //public float HitSize = 1;

    // These values here are used for testing purposes to see if it's working.
    // Basically a ripple in each axis direction, each with a different radius.

    void DoRayCast()
    {
        RaycastHit hitInfo;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hitInfo, 1000))
        {
            Debug.Log("did hit");
            //transform.position = hitInfo.point;
            //WaveEmit(hitInfo.point);
        }
    }

    void Update()
    {
        if (Time.time - lasetTriggerTime > 0.5f)
        {
            // decrease:
            scannedRemain -= Time.deltaTime * .2f;

        }
        else
        {
            // increase:
            scannedRemain += Time.deltaTime * .2f;

        }
        if (scannedRemain < 0) scannedRemain = 0;
        if (scannedRemain > 1) scannedRemain = 1;

        if (Input.GetMouseButtonDown(0))
        {
            //Debug.Log("clicked!");
            DoRayCast();
        }

        if (material == null) return;

        for (int i = 0; i < points.Length; i += 4)
        {
            float t = points[i + 3];

            int index = Mathf.FloorToInt((i + 1) / 4f);

            if (activeState[index])
            {
                t += Time.deltaTime * 0.5f;
                if (t > 1)
                {
                    // Lifetime Complete
                    // Create a new random point
                    t = 0;
                    activeState[index] = false;
                }
            }
            else
            {
                // do nothing casue it is not actived
            }


            // Set lifetime
            points[i + 3] = t;
        }

        material.SetFloatArray("_Points", points);
        material.SetFloat("_scannedRemainValue", scannedRemain);
    }

    public void WaveEmit()
    {
        var position = FindObjectOfType<HoloKitCameraManager>().transform.position;
        // add current position as origin position
        for (int i = 0; i < 6; i++)
        {
            if(activeState[i])
            {

            }
            else
            {
                lasetTriggerTime = Time.time;

                activeState[i] = true;
                points[i * 4] = position.x;
                points[i * 4 + 1] = position.y;
                points[i * 4 + 2] = position.z;
                points[i * 4 + 3] = 0;
                break;
            }
        }
    }

    public void WaveEmit(Vector3 position)
    {
        //var position = HoloKitCamera.Instance.CenterEyePose.position;
        // add current position as origin position
        for (int i = 0; i < 6; i++)
        {
            if (activeState[i])
            {

            }
            else
            {
                lasetTriggerTime = Time.time;
                activeState[i] = true;
                points[i * 4] = position.x;
                points[i * 4 + 1] = position.y;
                points[i * 4 + 2] = position.z;
                points[i * 4 + 3] = 0;
                break;
            }
        }
    }
}
