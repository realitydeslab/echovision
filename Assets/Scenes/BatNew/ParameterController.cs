using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ParameterController : MonoBehaviour
{
    public RectTransform transRootPanel;
    public Material matRipple;
    public RippleEffectEmitter rippleEmitter;
    public Volume volume;

    Dictionary<string, Action<float>> sliderActionList;

    void Start()
    {
        sliderActionList = new Dictionary<string, Action<float>>();
        sliderActionList.Add("RippleLifeTime", OnValueChaned_RippleLifeTime);
        sliderActionList.Add("RippleSpeed", OnValueChaned_RippleSpeed);
        sliderActionList.Add("BloomThreshold", OnValueChaned_BloomThreshold);
        sliderActionList.Add("BloomIndensity", OnValueChaned_BloomIndensity);


        for (int i=0; i<transRootPanel.childCount; i++)
        {
            Transform item = transRootPanel.GetChild(i);
            string param_name = item.Find("Label").GetComponent<TextMeshProUGUI>().text;
            Slider slider = item.Find("Slider").GetComponent<Slider>();
            TextMeshProUGUI display = item.Find("Value").GetComponent<TextMeshProUGUI>();
            display.text = slider.value.ToString("0.00");
            
            slider.onValueChanged.AddListener((float v) =>
            {
                display.text = v.ToString("0.00");
                if (item.name.Contains("MatItem"))
                {
                    matRipple.SetFloat(param_name, v);
                }
                else
                {
                    sliderActionList[param_name]?.Invoke(v);
                }
            });
            
        }
    }


    void OnValueChaned_RippleLifeTime(float v)
    {
        rippleEmitter.rippleLifeTime = v;
    }

    void OnValueChaned_RippleSpeed(float v)
    {
        rippleEmitter.rippleSpeed = v;
    }
    void OnValueChaned_BloomThreshold(float v)
    {
        VolumeProfile profile = volume.sharedProfile;
        Bloom bloom;
        profile.TryGet<Bloom>(out bloom);
        bloom.threshold.value = v;
    }
    void OnValueChaned_BloomIndensity(float v)
    {
        VolumeProfile profile = volume.sharedProfile;
        Bloom bloom;
        profile.TryGet<Bloom>(out bloom);
        bloom.intensity.value = v;
    }
}
