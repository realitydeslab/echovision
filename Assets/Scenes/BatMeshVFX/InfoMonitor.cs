using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;
using HoloKit;
using UnityEngine.InputSystem.XR;
using UnityEngine.VFX;

public class InfoMonitor : MonoBehaviour
{

    public Transform infoPanel;
    public GameObject infoPrefab;


    Transform tfHead;
    ARMeshManager m_MeshManager;

    void Start()
    {
        tfHead = FindObjectOfType<TrackedPoseDriver>().transform;
        m_MeshManager = FindObjectOfType<ARMeshManager>();
    }
    /*
    /// <summary>
    /// On awake, set up the mesh filter delegates.
    /// </summary>
    void Awake()
    {
        //m_BreakupMeshAction = new Action<MeshFilter>(BreakupMesh);
        //m_UpdateMeshAction = new Action<MeshFilter>(UpdateMesh);
        //m_RemoveMeshAction = new Action<MeshFilter>(RemoveMesh);
    }

    /// <summary>
    /// On enable, subscribe to the meshes changed event.
    /// </summary>
    void OnEnable()
    {
        Debug.Assert(m_MeshManager != null, "mesh manager cannot be null");
        m_MeshManager.meshesChanged += OnMeshesChanged;
    }

    /// <summary>
    /// On disable, unsubscribe from the meshes changed event.
    /// </summary>
    void OnDisable()
    {
        Debug.Assert(m_MeshManager != null, "mesh manager cannot be null");
        m_MeshManager.meshesChanged -= OnMeshesChanged;
    }

    /// <summary>
    /// When the meshes change, update the scene meshes.
    /// </summary>
    void OnMeshesChanged(ARMeshesChangedEventArgs args)
    {
        //if (args.added != null)
        //{
        //    args.added.ForEach(m_BreakupMeshAction);
        //}

        //if (args.updated != null)
        //{
        //    args.updated.ForEach(m_UpdateMeshAction);
        //}

        //if (args.removed != null)
        //{
        //    args.removed.ForEach(m_RemoveMeshAction);
        //}
    }
    */

    void Update()
    {
        IList<MeshFilter> mesh_list = m_MeshManager.meshes;

        if (mesh_list != null)
        {
            int mesh_count = mesh_list.Count;
            int vertex_count = 0;
            int triangle_count = 0;
            Vector3 min_pos = Vector3.zero;
            Vector3 max_pos = Vector3.zero;

            foreach (MeshFilter mesh in mesh_list)
            {
                vertex_count += mesh.sharedMesh.vertexCount;
                triangle_count += mesh.sharedMesh.triangles.Length / 3;
                min_pos = Vector3.Min(min_pos, mesh.sharedMesh.bounds.min);
                max_pos = Vector3.Max(max_pos, mesh.sharedMesh.bounds.max);
            }

            SetInfo("FPS", (1.0f / Time.smoothDeltaTime).ToString("0.0"));
            SetInfo("MeshCount", mesh_count.ToString());
            SetInfo("VertexCount", vertex_count.ToString());
            SetInfo("TriangleCount", triangle_count.ToString());
            SetInfo("BoundsMin", min_pos.ToString());
            SetInfo("BoundsMax", max_pos.ToString());
        }


        if (tfHead != null)
        {
            SetInfo("Head Pos", tfHead.position.ToString());
            SetInfo("Head Angle", tfHead.eulerAngles.ToString());
        }

        //Transform stereo = FindObjectOfType<HoloKitCameraManager>()?.transform.Find("Stereo Tracked Pose")?.Find("Center Eye Pose");
        //if(stereo != null)
        //{
        //    Transform left = stereo.Find("Left Eye Camera");
        //    SetInfo("Left Pos", left.position.ToString());
        //    SetInfo("Left Angle", left.eulerAngles.ToString());
        //    Transform right = stereo.Find("Right Eye Camera");
        //    SetInfo("Right Pos", right.position.ToString());
        //    SetInfo("Right Angle", right.eulerAngles.ToString());
        //    Transform black = stereo.Find("Black Camera");
        //    SetInfo("Black Pos", black.position.ToString());
        //    SetInfo("Black Angle", black.eulerAngles.ToString());
        //}
    }

    void SetInfo(string name, string text)
    {
        Transform item = infoPanel.Find(name);
        if (item == null)
        {
            item = Instantiate(infoPrefab, infoPanel).transform;
            item.name = name;
            item.Find("Label").GetComponent<TextMeshProUGUI>().text = name;
        }
        item.Find("Value").GetComponent<TextMeshProUGUI>().text = text;
    }
}
