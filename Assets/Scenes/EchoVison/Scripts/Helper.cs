using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;
using TMPro;
using System;


public class Helper : MonoBehaviour
{
    public bool controlPanelEnabled = false;
    public bool infoPanelEnabled = false;
    public bool extraInfoEnabled = false;

    [Header("Parameters")]
    public RectTransform controlPanelRoot;
    public SoundWaveEmitter soundwaveEmitter;
    public Material matMeshing;
    public VisualEffect vfxParticle;
    public Volume volumePostProcessing;

    [Header("Info")]
    public Transform infoPanelRoot;
    public GameObject infoPrefab;

    public Transform labelRoot;
    public GameObject labelPrefab;

    [Header("Extra")]
    public Transform extraPanelRoot;
    public TextMeshProUGUI textFPS;
    public RawImage humanTexture;


    Dictionary<string, Action<float>> sliderActionList;

    void Start()
    {
        controlPanelRoot.gameObject.SetActive(controlPanelEnabled);
        infoPanelRoot.gameObject.SetActive(infoPanelEnabled);
        extraPanelRoot.gameObject.SetActive(extraInfoEnabled);


        if(controlPanelEnabled)
        {
            controlPanelRoot.gameObject.SetActive(true);

            sliderActionList = new Dictionary<string, Action<float>>();
            for (int i = 0; i < controlPanelRoot.childCount; i++)
            {
                Transform item = controlPanelRoot.GetChild(i);
                if (item.gameObject.activeSelf == false)
                    continue;

                string param_name = item.Find("Label").GetComponent<TextMeshProUGUI>().text;
                Slider slider = item.Find("Slider").GetComponent<Slider>();
                TextMeshProUGUI display_value = item.Find("Value").GetComponent<TextMeshProUGUI>();

                // refresh slider
                if (item.name.ToLower().Contains("mat"))
                {
                    slider.value = matMeshing.GetFloat(param_name);
                }
                else if (item.name.ToLower().Contains("vfx"))
                {
                    slider.value = vfxParticle.GetFloat(param_name);
                }

                // refresh text
                display_value.text = slider.value.ToString("0.00");


                // register slider
                slider.onValueChanged.AddListener((float v) =>
                {
                    display_value.text = v.ToString("0.00");

                    SliderCallbackFunction(item.name,param_name, v);
                });
            }
        }
    }

    void Update()
    {
        if (extraInfoEnabled)
        {
            textFPS.text = "FPS: " + (1.0f / Time.smoothDeltaTime).ToString("0.0");
        }
    }


    void SliderCallbackFunction(string item_name, string param_name, float v)
    {
        if (item_name.ToLower().Contains("mat"))
        {
            matMeshing.SetFloat(param_name, v);
        }
        else if (item_name.ToLower().Contains("vfx"))
        {
            vfxParticle.SetFloat(param_name, v);
        }
        else
        {
            sliderActionList[param_name]?.Invoke(v);
        }
    }


    public void AddSliderAction(string name, Action<float> action)
    {
        sliderActionList.Add(name, action);
    }

    public void SetInfo(string name, string text)
    {
        if (infoPanelEnabled == false) return;

        Transform item = infoPanelRoot.Find(name);
        if (item == null)
        {
            item = Instantiate(infoPrefab, infoPanelRoot).transform;
            item.name = name;
            item.Find("Label").GetComponent<TextMeshProUGUI>().text = name;
        }
        item.Find("Value").GetComponent<TextMeshProUGUI>().text = text;
    }

    void RemoveInfo(string name)
    {
        if (infoPanelEnabled == false) return;

        Transform item = infoPanelRoot.Find(name);
        if (item != null)
        {
            Destroy(item.gameObject);
        }
    }

    public void SetLabel(string name, Vector3 pos, string text)
    {
        if (infoPanelEnabled == false) return;

        Transform item = labelRoot.Find(name);
        if (item == null)
        {
            item = Instantiate(labelPrefab, labelRoot).transform;
            item.name = name;
        }
        item.GetComponent<TextMesh>().text = text;
        item.position = pos;
        item.transform.rotation = Quaternion.LookRotation((pos - GameManager.Instance.HeadTransform.position), Vector3.up);
    }

    public void SetHumanTexture(Texture tex)
    {
        if (extraInfoEnabled == false) return;

        if (humanTexture.gameObject.activeSelf == false)
            humanTexture.gameObject.SetActive(true);

        humanTexture.texture = tex;
    }

}
