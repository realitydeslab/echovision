using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class BatSoundSceneManager : MonoBehaviour
{
    //public bool _arPlaneManagerState = false;
    public bool _arMeshManagerState = false;
    //public Button _planeButton;
    public Button _meshButton;

    //public ARPlaneManager ARPlaneManager;
    public ARMeshManager ARMeshManager;

    //TMPro.TMP_Text _planeText;
    TMPro.TMP_Text _meshText;

    public Slider mainSlider;

    public void Start()
    {
        //Adds a listener to the main slider and invokes a method when the value changes.
        mainSlider.onValueChanged.AddListener(delegate { ValueChangeCheck(); });

        //_planeText = _planeButton.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
        _meshText = _meshButton.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
        //Debug.Log(_planeText);
        Debug.Log(_meshText);
    }

    // Invoked when the value of the slider changes.
    public void ValueChangeCheck()
    {
        // Debug.Log(mainSlider.value);
        FindObjectOfType<ARMeshManager>().density = mainSlider.value;
    }

    public void OnOffARPlaneManager()
    {
        //if (_arPlaneManagerState)
        //{
        //    ARPlaneManager.enabled = false;
        //    _arPlaneManagerState = false;
        //    //_planeButton.transform.GetChild(0).GetComponent<TMPro.TextMeshPro>().text = "Plane Off";
        //    _planeText.text = "Plane Off";
        //}
        //else
        //{
        //    ARPlaneManager.enabled = true;
        //    _arPlaneManagerState = true;
        //    //_planeButton.transform.GetChild(0).GetComponent<TMPro.TextMeshPro>().text = "Plane On";
        //    _planeText.text = "Plane On";

        //}
    }

    public void OnAndOffMeshing()
    {
        if (_arMeshManagerState)
        {
            ARMeshManager.enabled = false;
            //Debug.Log(FindObjectOfType<ARMeshManager>().name);
            _arMeshManagerState = false;
            _meshText.text = "Mesh Off";

        }
        else
        {
            ARMeshManager.enabled = true;
            _arMeshManagerState = true;
            _meshText.text = "Mesh On";

        }
    }
}



