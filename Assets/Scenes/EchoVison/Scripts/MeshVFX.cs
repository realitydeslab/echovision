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

public class MeshVFX : MonoBehaviour
{
    
    public VisualEffect vfx;

    public Transform infoPanel;
    public GameObject infoPrefab;

    public TextMeshProUGUI textFPS;

    ARMeshManager m_MeshManager;

    private const int BUFFER_STRIDE = 12; // 12 Bytes for a Vector3 (4,4,4)
    private static readonly int VfxBufferProperty = Shader.PropertyToID("MeshPointCache");

    private int bufferInitialCapacity = 64000;
    //[SerializeField] private VisualEffect visualEffect;
    private List<Vector3> data;
    private GraphicsBuffer graphicsBuffer;

    private static readonly int VfxBufferPropertyNormal = Shader.PropertyToID("MeshNormalCache");
    private List<Vector3> dataNormal;
    private GraphicsBuffer graphicsBufferNormal;

    void Start()
    {
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

    void LateUpdate()
    {
        textFPS.text = "FPS: " + (1.0f / Time.smoothDeltaTime).ToString("0.0");

        IList<MeshFilter> mesh_list = m_MeshManager.meshes;

        if(mesh_list != null)
        {
            data.Clear();
            dataNormal.Clear();
            int mesh_count = mesh_list.Count;
            int vertex_count = 0;
            Vector3 min_pos = Vector3.zero;
            Vector3 max_pos = Vector3.zero;

            foreach (MeshFilter mesh in mesh_list)
            {
                min_pos = Vector3.Min(min_pos, mesh.sharedMesh.bounds.min);
                max_pos = Vector3.Max(max_pos, mesh.sharedMesh.bounds.max);

                vertex_count += mesh.sharedMesh.vertexCount;

                data.AddRange(mesh.sharedMesh.vertices);
                dataNormal.AddRange(mesh.sharedMesh.normals);
            }

            if (vertex_count > bufferInitialCapacity)
            {
                data.RemoveRange(bufferInitialCapacity, vertex_count - bufferInitialCapacity);
                dataNormal.RemoveRange(bufferInitialCapacity, vertex_count - bufferInitialCapacity);
            }

            //if (vertex_count > bufferInitialCapacity)
            //{
            //    for (int i = 0; i < vertex_count - bufferInitialCapacity; i++)
            //    {
            //        data.RemoveAt(Random.Range(0, data.Count));
            //    }
            //}

            //vfx.SetVector3("BoundsMin", min_pos);
            //vfx.SetVector3("BoundsMax", max_pos);


            // Set Buffer data, but before that ensure there is enough capacity
            EnsureBufferCapacity(ref graphicsBuffer, data.Count, BUFFER_STRIDE, vfx, VfxBufferProperty);
            graphicsBuffer.SetData(data);

            EnsureBufferCapacity(ref graphicsBufferNormal, dataNormal.Count, BUFFER_STRIDE, vfx, VfxBufferPropertyNormal);
            graphicsBufferNormal.SetData(dataNormal);

            vfx.SetInt("MeshPointCount", data.Count);
            
        }
        //else
        //{
        //    data.Clear();
        //    data.Add(Vector3.zero);
        //    graphicsBuffer.SetData(data);
        //}
    }

    // 
    // https://forum.unity.com/threads/vfx-graph-siggraph-2021-video.1198156/
    void Awake()
    {
        // List with data used to fill buffer
        data = new List<Vector3>(bufferInitialCapacity);
        // Create initial graphics buffer
        EnsureBufferCapacity(ref graphicsBuffer, bufferInitialCapacity, BUFFER_STRIDE, vfx, VfxBufferProperty);

        dataNormal = new List<Vector3>(bufferInitialCapacity);
        EnsureBufferCapacity(ref graphicsBufferNormal, bufferInitialCapacity, BUFFER_STRIDE, vfx, VfxBufferPropertyNormal);
    }

    

    void OnDestroy()
    {
        ReleaseBuffer(ref graphicsBuffer);

        ReleaseBuffer(ref graphicsBufferNormal);
    }

    private void EnsureBufferCapacity(ref GraphicsBuffer buffer, int capacity, int stride, VisualEffect _vfx, int vfxProperty)
    {
        // Reallocate new buffer only when null or capacity is not sufficient
        if (buffer == null) // || buffer.count < capacity) // remove dynamic allocating function
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
}
