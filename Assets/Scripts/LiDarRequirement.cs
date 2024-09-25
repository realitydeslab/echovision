using UnityEngine;
using UnityEngine.UI;

public class LiDarRequirement : MonoBehaviour
{
    [SerializeField]
    GameObject instructionPanel;
    
    void Start()
    {
        if (instructionPanel == null) return;

        bool supported = HoloKit.iOS.DeviceData.SupportLiDAR();

        instructionPanel.SetActive(!supported);
    }
}
