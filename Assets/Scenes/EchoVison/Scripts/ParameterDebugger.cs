using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

public class ParameterDebugger : MonoBehaviour
{
    public bool enabled = false;
    public RectTransform transRootPanel;
    public Volume volume;

    public Material mat;
    public VisualEffect vfx;
    public SoundWaveEmitter soundwaveEmitter;

    public TextMeshProUGUI textFPS;

    Dictionary<string, Action<float>> sliderActionList;

    void Start()
    {
        if(enabled == false)
        {
            transRootPanel.gameObject.SetActive(false);
            return;
        }
        else
        {
            transRootPanel.gameObject.SetActive(true);
        }

        sliderActionList = new Dictionary<string, Action<float>>();
        sliderActionList.Add("DebugMode", OnValueChaned_DebugMode);
        sliderActionList.Add("TestAge", OnValueChaned_TestAge);
        sliderActionList.Add("TestRange", OnValueChaned_TestRange);

        sliderActionList.Add("Life", OnValueChaned_Life);
        sliderActionList.Add("Speed", OnValueChaned_Speed);
        sliderActionList.Add("Angle", OnValueChaned_Angle);

        sliderActionList.Add("MinimumThickness", OnValueChaned_MinimumThicknesss);

        //sliderActionList.Add("ParticleSpeed", OnValueChaned_ParticleSpeed);
        //sliderActionList.Add("ParticleDamp", OnValueChaned_ParticleDamp);
        //sliderActionList.Add("ParicleLifeTime", OnValueChaned_ParicleLifeTime);
        //sliderActionList.Add("EmitParticleCount", OnValueChaned_EmitParticleCount);

        for (int i = 0; i < transRootPanel.childCount; i++)
        {
            Transform item = transRootPanel.GetChild(i);
            if (item.gameObject.activeSelf == false)
                continue;
            string param_name = item.Find("Label").GetComponent<TextMeshProUGUI>().text;
            Slider slider = item.Find("Slider").GetComponent<Slider>();
            TextMeshProUGUI display = item.Find("Value").GetComponent<TextMeshProUGUI>();

            if(item.name.Contains("VFXItem"))
            {
                slider.value = vfx.GetFloat(param_name);
            }
            else if(item.name.Contains("MatItem"))
            {
                slider.value = mat.GetFloat(param_name);
            }


            display.text = slider.value.ToString("0.00");

            slider.onValueChanged.AddListener((float v) =>
            {
                display.text = v.ToString("0.00");
                if (item.name.Contains("MatItem"))
                {
                    mat.SetFloat(param_name, v);
                }
                else if (item.name.Contains("VFXItem"))
                {
                    vfx.SetFloat(param_name, v);
                }
                else
                {
                    sliderActionList[param_name]?.Invoke(v);
                }
                
            });

        }
    }

    private void Update()
    {
        textFPS.text = "FPS: " + (1.0f / Time.smoothDeltaTime).ToString("0.0");
    }

    void OnValueChaned_DebugMode(float v)
    {
        soundwaveEmitter.debugMode = v > 0.5f ? true : false;
    }

    void OnValueChaned_TestAge(float v)
    {
        soundwaveEmitter.testAge = v;
    }

    void OnValueChaned_TestRange(float v)
    {
        soundwaveEmitter.testRange = v;
    }

    void OnValueChaned_Life(float v)
    {
        soundwaveEmitter.soundwaveLife = new Vector2(v, v*1.5f);
    }
    void OnValueChaned_Speed(float v)
    {
        soundwaveEmitter.soundwaveSpeed = new Vector2(v, v * 1.5f);
    }
    void OnValueChaned_Angle(float v)
    {
        soundwaveEmitter.soundwaveAngle = new Vector2(v, v * 1.5f);
    }
    void OnValueChaned_MinimumThicknesss(float v)
    {
        soundwaveEmitter.minWaveThickness = v;
    }
    

}
