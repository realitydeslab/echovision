using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoHideMesh : MonoBehaviour
{
    
    void Start()
    {
#if UNITY_IOS && !UNITY_EDITOR
        foreach(Transform trans in transform)
        {
            Destroy(trans.gameObject);
        }
#endif
    }

}
