// SPDX-FileCopyrightText: Copyright 2024-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT
using UnityEngine;
using UnityEngine.UI;

public class LIDARRequirement : MonoBehaviour
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
