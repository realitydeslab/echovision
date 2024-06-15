using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;
using TMPro;
using System;


public class HelperModule : GenericSingleton<HelperModule>
{

    [SerializeField] bool isVisible = false;
    public bool Visible
    {
        get => isVisible;
        set
        {
            isVisible = value;
            SetDebugPanelVisible(value);
        }
    }

    enum HelperItemType
    {
        Info,
        Slider,
        SpaceInfo
    }

    [Header("Info Panel")]
    [SerializeField] private Transform rootInfo;
    [SerializeField] private GameObject prefabInfo;
    Dictionary<string, GameObject> infoList;

    [Header("Slider Panel")]
    [SerializeField] private Transform rootSlider;
    [SerializeField] private GameObject prefabSlider;
    Dictionary<string, GameObject> sliderList;

    [Header("SpaceInfo Panel")]
    [SerializeField] private Transform rootSpaceInfo;
    [SerializeField] private GameObject prefabSpaceInfo;
    Dictionary<string, GameObject> spaceInfoList;

    [Header("UI System")]
    [SerializeField] private Button buttonTogglePanel;
    [SerializeField] private Transform transTabs;

    [SerializeField] private Transform transInfoPanel;
    [SerializeField] private Transform transSliderPanel;
    [SerializeField] private Transform transSpaceInfoPanel;
    
    [SerializeField] private Button buttonInfo;
    [SerializeField] private Button buttonSlider;
    [SerializeField] private Button buttonSpaceInfo;

    

    #region public functions
    public void ToggleDebugPanel()
    {
        if (enabled == false) return;

        isVisible = !isVisible;
        SetDebugPanelVisible(isVisible);
    }

    public void SetDebugPanelVisible(bool state)
    {
        if (enabled == false) return;

        transTabs.gameObject.SetActive(state);
        if (state == false)
        {
            HidePanel(HelperItemType.Info);
            HidePanel(HelperItemType.Slider);
            HidePanel(HelperItemType.SpaceInfo);
        }
        else
        {
            ShowPanel(HelperItemType.Info);
        }
    }

    public void SetInfo(string name, string info)
    {
        if (enabled == false) return;

        GameObject go = null;

        if (!infoList.ContainsKey(name))
        {
            go = CreateGameObject(name, HelperItemType.Info);

            infoList.Add(name, go);
        }
        else go = infoList[name];


        TextMeshProUGUI text_ugui = GetUITextComponent(go);
        text_ugui.text = info;
    }

    public void DeleleInfo(string name, string info)
    {
        if (enabled == false) return;

        if (infoList.ContainsKey(name))
        {
            Destroy(infoList[name]);
            infoList.Remove(name);
        }
    }

    public void SetSlider(string name, Action<float> action)
    {
        if (enabled == false) return;

        GameObject go = null;

        if (!infoList.ContainsKey(name))
        {
            go = CreateGameObject(name, HelperItemType.Slider);

            sliderList.Add(name, go);
        }
        else go = infoList[name];

        TextMeshProUGUI value_text = GetUITextComponent(go);
        Slider slider = GetSliderComponent(go);
        slider.onValueChanged.AddListener((float v) =>
        {
            value_text.text = v.ToString("0.00");

            action?.Invoke(v);
        });
    }

    public void DeleteSlider(string name)
    {
        if (enabled == false) return;

        if (sliderList.ContainsKey(name))
        {
            Slider slider = GetSliderComponent(sliderList[name]);
            slider.onValueChanged.RemoveAllListeners();
            Destroy(sliderList[name]);
            sliderList.Remove(name);
        }
    }

    public void SetSapceInfo(string name, Transform trans, string info)
    {
        if (enabled == false) return;

        GameObject go = null;

        if (!spaceInfoList.ContainsKey(name))
        {
            go = CreateGameObject(name, HelperItemType.SpaceInfo);

            spaceInfoList.Add(name, go);
        }
        else go = infoList[name];

        go.transform.SetPositionAndRotation(trans.position, trans.rotation);
        TextMesh text_space = GetSpaceTextComponent(go);
        text_space.text = info;
    }

    public void DeleleSapceInfo(string name)
    {
        if (enabled == false) return;

        if (spaceInfoList.ContainsKey(name))
        {
            Destroy(spaceInfoList[name]);
            spaceInfoList.Remove(name);
        }
    }
    #endregion

    #region private functions

    void Start()
    {
        InitializeUI();
        SetDebugPanelVisible(isVisible);
    }

    void InitializeUI()
    {
        buttonTogglePanel.onClick.AddListener(ToggleDebugPanel);

        buttonInfo.onClick.AddListener(delegate { ShowPanel(HelperItemType.Info); });
        buttonSlider.onClick.AddListener(delegate { ShowPanel(HelperItemType.Slider); });
        buttonSpaceInfo.onClick.AddListener(delegate { ShowPanel(HelperItemType.SpaceInfo); });
    }

    void ShowPanel(HelperItemType type)
    {
        transInfoPanel.gameObject.SetActive(false);
        transSliderPanel.gameObject.SetActive(false);
        transSpaceInfoPanel.gameObject.SetActive(false);

        Transform trans_shown = null;
        if (type == HelperItemType.Info) trans_shown = transInfoPanel;
        else if (type == HelperItemType.Slider) trans_shown = transSliderPanel;
        else if (type == HelperItemType.SpaceInfo) trans_shown = transSpaceInfoPanel;

        trans_shown.gameObject.SetActive(true);
    }
    void HidePanel(HelperItemType type)
    {
        Transform trans_shown = null;
        if (type == HelperItemType.Info) trans_shown = transInfoPanel;
        else if (type == HelperItemType.Slider) trans_shown = transSliderPanel;
        else if (type == HelperItemType.SpaceInfo) trans_shown = transSpaceInfoPanel;

        trans_shown.gameObject.SetActive(false);
    }    

    GameObject CreateGameObject(string name, HelperItemType type)
    {
        GameObject new_go = null;

        if(type == HelperItemType.Info)
        {
            new_go = Instantiate(prefabInfo, rootInfo);
        }            
        else if (type == HelperItemType.Slider)
        {
            new_go = Instantiate(prefabSlider, rootSlider);            
        }            
        else if (type == HelperItemType.SpaceInfo)
        {
            new_go = Instantiate(prefabSpaceInfo, rootSpaceInfo);
        }

        if(new_go != null)
        {
            new_go.name = name;
            if(type == HelperItemType.Info || type == HelperItemType.Slider)
            {
                new_go.transform.Find("Label").GetComponent<TextMeshProUGUI>().text = name;
                new_go.transform.Find("Value").GetComponent<TextMeshProUGUI>().text = "";
            }
            else if(type == HelperItemType.SpaceInfo)
            {
                new_go.transform.Find("Value").GetComponent<TextMesh>().text = "";
            }
        }

        return new_go;
    }

    TextMeshProUGUI GetUITextComponent(GameObject go)
    {
        return go.transform.Find("Value").GetComponent<TextMeshProUGUI>();
    }

    Slider GetSliderComponent(GameObject go)
    {
        return go.transform.Find("Slider").GetComponent<Slider>();
    }

    TextMesh GetSpaceTextComponent(GameObject go)
    {
        return go.transform.Find("Value").GetComponent<TextMesh>();
    }
    #endregion
}
