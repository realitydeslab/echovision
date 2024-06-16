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
using UnityEngine.XR.ARFoundation.Samples;

public class MeshVFX : MonoBehaviour
{
    [SerializeField]
    private VisualEffect vfx;


    public int bufferInitialCapacity = 64000;
    public bool dynamicallyResizeBuffer = false;

    private const int BUFFER_STRIDE = 12; // 12 Bytes for a Vector3 (4,4,4)

    private static readonly int VertexBufferPropertyID = Shader.PropertyToID("MeshPointCache");
    private List<Vector3> listVertex;
    private GraphicsBuffer bufferVertex;

    private static readonly int NormalBufferPropertyID = Shader.PropertyToID("MeshNormalCache");
    private List<Vector3> listNormal;
    private GraphicsBuffer bufferNormal;

    List<(float, int)> listMeshDistance = new List<(float, int)>();
    List<int> listRandomIndex = new List<int>();

    [SerializeField]
    ARMeshManager meshManager;

    [SerializeField]
    HoloKitCameraManager cameraManager; 

     [SerializeField] TrackedPoseDriver trackedPoseDriver;

    void Start()
    {
        
    }
    

    void LateUpdate()
    {
        //ShowDebugInfo();

        IList<MeshFilter> mesh_list = meshManager.meshes;

        if(mesh_list != null)
        {
            listVertex.Clear();
            listNormal.Clear();

            int mesh_count = mesh_list.Count; 
            int vertex_count = 0;
            int triangle_count = 0;
            //Vector3 head_pos = GameManager.Instance.HeadTransform.position;
            //Vector3 head_pos = cameraManager.CenterEyePose.position;
            Vector3 head_pos = trackedPoseDriver.transform.position;
            float distance = 0;
            Vector3 min_pos = Vector3.zero;
            Vector3 max_pos = Vector3.zero;

            if(dynamicallyResizeBuffer)
            {
                // randomize the order of mesh
                listRandomIndex.Clear();
                for (int i = 0; i < mesh_list.Count; i++)
                {
                    listRandomIndex.Add(i);
                }

                int n = listRandomIndex.Count;
                while (n > 1)
                {
                    n--;
                    int k = Random.Range(0, n + 1);
                    int value = listRandomIndex[k];
                    listRandomIndex[k] = listRandomIndex[n];
                    listRandomIndex[n] = value;
                }

                // push to buffer
                for (int i = 0; i < listRandomIndex.Count; i++)
                {
                    int index = listRandomIndex[i];
                    MeshFilter mesh = mesh_list[index];

                    listVertex.AddRange(mesh.sharedMesh.vertices);
                    listNormal.AddRange(mesh.sharedMesh.normals);

                    vertex_count += mesh.sharedMesh.vertexCount;
                    triangle_count += mesh.sharedMesh.triangles.Length / 3;

                    min_pos = Vector3.Min(min_pos, mesh.sharedMesh.bounds.min);
                    max_pos = Vector3.Max(max_pos, mesh.sharedMesh.bounds.max);
                }
            }
            else
            {
                // sort all meshes by distance
                listMeshDistance.Clear();
                for (int i = 0; i < mesh_list.Count; i++)
                {
                    MeshFilter mesh = mesh_list[i];

                    distance = Vector3.Distance(head_pos, mesh.sharedMesh.bounds.center);

                    listMeshDistance.Add((distance, i));
                }
                listMeshDistance.Sort((x, y) => x.Item1.CompareTo(y.Item1));


                // push nearest to buffer
                for (int i = 0; i < listMeshDistance.Count; i++)
                {
                    int index = listMeshDistance[i].Item2;
                    MeshFilter mesh = mesh_list[index];

                    listVertex.AddRange(mesh.sharedMesh.vertices);
                    listNormal.AddRange(mesh.sharedMesh.normals);

                    vertex_count += mesh.sharedMesh.vertexCount;
                    triangle_count += mesh.sharedMesh.triangles.Length / 3;

                    min_pos = Vector3.Min(min_pos, mesh.sharedMesh.bounds.min);
                    max_pos = Vector3.Max(max_pos, mesh.sharedMesh.bounds.max);

                    if (vertex_count > bufferInitialCapacity)
                        break;
                }

                if (vertex_count > bufferInitialCapacity)
                {
                    listVertex.RemoveRange(bufferInitialCapacity, vertex_count - bufferInitialCapacity);
                    listNormal.RemoveRange(bufferInitialCapacity, vertex_count - bufferInitialCapacity);
                }
            }

            


            // Set Buffer data, but before that ensure there is enough capacity
            EnsureBufferCapacity(ref bufferVertex, listVertex.Count, BUFFER_STRIDE, vfx, VertexBufferPropertyID);
            bufferVertex.SetData(listVertex);

            EnsureBufferCapacity(ref bufferNormal, listNormal.Count, BUFFER_STRIDE, vfx, NormalBufferPropertyID);
            bufferNormal.SetData(listNormal);


            // Push Changes to VFX
            vfx.SetInt("MeshPointCount", listVertex.Count);
            //vfx.SetVector3("BoundsMin", min_pos);
            //vfx.SetVector3("BoundsMax", max_pos);

        }

    }

    // 
    // https://forum.unity.com/threads/vfx-graph-siggraph-2021-video.1198156/
    void Awake()
    {

        // Create initial graphics buffer
        listVertex = new List<Vector3>(bufferInitialCapacity);
        EnsureBufferCapacity(ref bufferVertex, bufferInitialCapacity, BUFFER_STRIDE, vfx, VertexBufferPropertyID);

        listNormal = new List<Vector3>(bufferInitialCapacity);
        EnsureBufferCapacity(ref bufferNormal, bufferInitialCapacity, BUFFER_STRIDE, vfx, NormalBufferPropertyID);
    }

    void ShowDebugInfo()
    {
        IList<MeshFilter> mesh_list = meshManager.meshes;

        if (mesh_list == null) return;


        int mesh_count = mesh_list.Count;
        HelperModule.Instance.SetInfo("MeshCount", mesh_count.ToString());

        int vertex_count = 0;
        int triangle_count = 0;
        //Vector3 head_pos = cameraManager.CenterEyePose.position;
        Vector3 head_pos = trackedPoseDriver.transform.position;
        float distance = 0;
        Vector3 min_pos = Vector3.zero;
        Vector3 max_pos = Vector3.zero;
        for (int i = 0; i < mesh_list.Count; i++)
        {
            MeshFilter mesh = mesh_list[i];

            vertex_count += mesh.sharedMesh.vertexCount;
            triangle_count += mesh.sharedMesh.triangles.Length / 3;

            min_pos = Vector3.Min(min_pos, mesh.sharedMesh.bounds.min);
            max_pos = Vector3.Max(max_pos, mesh.sharedMesh.bounds.max);

            distance = Vector3.Distance(head_pos, mesh.sharedMesh.bounds.center);

            //GameManager.Instance.SetInfo("Mesh" + i.ToString(), string.Format("VerCount:{0}, Dis:{1}, Min:{2}, Max:{3}", mesh.sharedMesh.vertexCount, distance.ToString("0.000"), min_pos, max_pos));
            //GameManager.Instance.SetLabel(i.ToString(), mesh.sharedMesh.bounds.center, i.ToString() + "|" + distance.ToString("0.00"));
        }

        HelperModule.Instance.SetInfo("VertexCount", vertex_count.ToString());
        HelperModule.Instance.SetInfo("TriangleCount", triangle_count.ToString());
        HelperModule.Instance.SetInfo("BoundsMin", min_pos.ToString());
        HelperModule.Instance.SetInfo("BoundsMax", max_pos.ToString());
        HelperModule.Instance.SetInfo("Center", ((min_pos + max_pos) * 0.5f).ToString());
    }

    void OnDestroy()
    {
        ReleaseBuffer(ref bufferVertex);

        ReleaseBuffer(ref bufferNormal);
    }

    private void EnsureBufferCapacity(ref GraphicsBuffer buffer, int capacity, int stride, VisualEffect _vfx, int vfxProperty)
    {
        // Reallocate new buffer only when null or capacity is not sufficient
        if (buffer == null || (dynamicallyResizeBuffer && buffer.count < capacity)) // remove dynamic allocating function
        {
            Debug.Log("Graphic Buffer reallocated!");
            // Buffer memory must be released
            buffer?.Release();
            // Vfx Graph uses structured buffer
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, stride);
            // Update buffer referenece
            _vfx.SetGraphicsBuffer(vfxProperty, buffer);
        }
    }

    private void ReleaseBuffer(ref GraphicsBuffer buffer)
    {
        // Buffer memory must be released
        buffer?.Release();
        buffer = null;
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
}
