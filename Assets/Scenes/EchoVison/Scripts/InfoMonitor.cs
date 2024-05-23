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

    public Transform labelRoot; 
    public GameObject labelPrefab;


    Transform tfHead;
    ARMeshManager m_MeshManager;
    SDFTexture sdfTexture;

    void Start()
    {
        tfHead = FindObjectOfType<TrackedPoseDriver>().transform;
        m_MeshManager = FindObjectOfType<ARMeshManager>();

        sdfTexture = FindObjectOfType<SDFTexture>();
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
        SetInfo("FPS", (1.0f / Time.smoothDeltaTime).ToString("0.0"));

        //SetInfo("Volume", MicInput.MicLoudness.ToString("0.000"));

        ShowMeshInfo();
    }

    List<(float, int)> distanceList = new List<(float, int)>();

    void ShowMeshInfo()
    {

        
        IList<MeshFilter> mesh_list = m_MeshManager.meshes;

        if (mesh_list != null)
        {
            int mesh_count = mesh_list.Count;
            SetInfo("MeshCount", mesh_count.ToString());

            //SetInfo("voxelBounds", sdfTexture.voxelBounds.ToString());
            //SetInfo("voxelResolution", sdfTexture.voxelResolution.ToString());
            //SetInfo("voxelSize", sdfTexture.voxelSize.ToString());
            //SetInfo("resolution", sdfTexture.resolution.ToString());

            int vertex_count = 0;
            int triangle_count = 0;
            Vector3 min_pos = Vector3.zero;
            Vector3 max_pos = Vector3.zero;

            distanceList.Clear();

            for (int i=0; i<mesh_list.Count; i++)
            {
                MeshFilter mesh = mesh_list[i];
                vertex_count += mesh.sharedMesh.vertexCount;
                triangle_count += mesh.sharedMesh.triangles.Length / 3;
                min_pos = Vector3.Min(min_pos, mesh.sharedMesh.bounds.min);
                max_pos = Vector3.Max(max_pos, mesh.sharedMesh.bounds.max);
                float dis = Vector3.Distance(tfHead.transform.position, mesh.sharedMesh.bounds.center);

                SetLabel(i.ToString(), mesh.sharedMesh.bounds.center, i.ToString() + "|"+dis.ToString("0.00"));

                distanceList.Add((dis, i));
            }

            distanceList.Sort((x, y) => y.Item1.CompareTo(x.Item1));

            for(int i=0; i<distanceList.Count; i++)
            {
                int index  = distanceList[i].Item2;
                MeshFilter mesh = mesh_list[index];
                SetInfo("Mesh" + i.ToString(), string.Format("{0} | VerCount:{1},TriCount:{2}, Dis:{3}", index, mesh.sharedMesh.vertexCount, (mesh.sharedMesh.triangles.Length / 3), distanceList[i].Item1.ToString("0.000")));
            }

            


            SetInfo("VertexCount", vertex_count.ToString());
            SetInfo("TriangleCount", triangle_count.ToString());
            SetInfo("BoundsMin", min_pos.ToString());
            SetInfo("BoundsMax", max_pos.ToString());
            SetInfo("Center", ((min_pos+max_pos)*0.5f).ToString());

            //sdfTexture.transform.position = ((min_pos + max_pos) * 0.5f);
            //sdfTexture.transform.localScale = (max_pos - min_pos);
        }

        

        //if (tfHead != null)
        //{
        //    SetInfo("Head Pos", tfHead.position.ToString());
        //    SetInfo("Head Angle", tfHead.eulerAngles.ToString());
        //}

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

    void RemoveInfo(string name)
    {
        Transform item = infoPanel.Find(name);
        if (item != null)
        {
            Destroy(item.gameObject);
        }
    }

    void SetLabel(string name, Vector3 pos, string text)
    {
        Transform item = labelRoot.Find(name);
        if (item == null)
        {
            item = Instantiate(labelPrefab, labelRoot).transform;
            item.name = name;
        }
        item.GetComponent<TextMesh>().text = text;
        item.position = pos;
        item.transform.rotation = Quaternion.LookRotation((pos - tfHead.position), Vector3.up);
    }
}
