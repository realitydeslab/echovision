using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;
#if UNITY_IOS
using HoloKit;
#endif
using UnityEngine.InputSystem.XR;
using UnityEngine.VFX;

public class MeshVFX : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] ARMeshManager meshManager;
#if UNITY_IOS
    [SerializeField] TrackedPoseDriver trackedPoseDriver;
#elif UNITY_VISIONOS
    [SerializeField] UnityEngine.SpatialTracking.TrackedPoseDriver trackedPoseDriver;
#endif

    [SerializeField] VisualEffect vfx;

    [Header("Buffer Settings")]
    [SerializeField] int bufferInitialCapacity = 64000;
    [SerializeField] bool dynamicallyResizeBuffer = false;
    private const int BUFFER_STRIDE = 12; // 12 Bytes for a Vector3 (4,4,4)

    string vertexBufferPropertyName = "MeshPointCache";
    List<Vector3> listVertex;
    GraphicsBuffer bufferVertex;
    
    string normalBufferPropertyName = "MeshNormalCache";
    List<Vector3> listNormal;
    GraphicsBuffer bufferNormal;

    List<(float, int)> listMeshDistance = new List<(float, int)>();
    List<int> listRandomIndex = new List<int>();

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
            EnsureBufferCapacity(ref bufferVertex, listVertex.Count, BUFFER_STRIDE, vfx, vertexBufferPropertyName);
            bufferVertex.SetData(listVertex);

            EnsureBufferCapacity(ref bufferNormal, listNormal.Count, BUFFER_STRIDE, vfx, normalBufferPropertyName);
            bufferNormal.SetData(listNormal);


            // Push Changes to VFX
            vfx.SetInt("MeshPointCount", listVertex.Count);


            // Push Transform to VFX
            // As meshes may not locate at (0,0,0) like they did in iOS.
            // We need to push transform into VFX for converting local position to world position
            if (mesh_list.Count > 0)
            {
                vfx.SetVector3("MeshTransform_position", mesh_list[0].transform.position);
                vfx.SetVector3("MeshTransform_angles", mesh_list[0].transform.rotation.eulerAngles);
                vfx.SetVector3("MeshTransform_scale", mesh_list[0].transform.localScale);
            }
            else
            {
                vfx.SetVector3("MeshTransform_position", Vector3.zero);
                vfx.SetVector3("MeshTransform_angles", Vector3.zero);
                vfx.SetVector3("MeshTransform_scale", Vector3.one);
            }
        }
    }

    private void EnsureBufferCapacity(ref GraphicsBuffer buffer, int capacity, int stride, VisualEffect _vfx, string vfxProperty)
    {
        // Reallocate new buffer only when null or capacity is not sufficient
        if (buffer == null || (dynamicallyResizeBuffer && buffer.count < capacity)) // remove dynamic allocating function
        {
            //Debug.Log("Graphic Buffer reallocated!");
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

    // 
    // https://forum.unity.com/threads/vfx-graph-siggraph-2021-video.1198156/
    void Awake()
    {

        // Create initial graphics buffer
        listVertex = new List<Vector3>(bufferInitialCapacity);
        EnsureBufferCapacity(ref bufferVertex, bufferInitialCapacity, BUFFER_STRIDE, vfx, vertexBufferPropertyName);

        listNormal = new List<Vector3>(bufferInitialCapacity);
        EnsureBufferCapacity(ref bufferNormal, bufferInitialCapacity, BUFFER_STRIDE, vfx, normalBufferPropertyName);
    }

    void ShowDebugInfo()
    {
        IList<MeshFilter> mesh_list = meshManager.meshes;

        if (mesh_list == null) return;

        int mesh_count = mesh_list.Count;
        int vertex_count = 0;
        int triangle_count = 0;
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

            //HelperModule.Instance.SetInfo("Mesh" + i.ToString(), string.Format("VerCount:{0}, Dis:{1}, Min:{2}, Max:{3}", mesh.sharedMesh.vertexCount, distance.ToString("0.000"), min_pos, max_pos));
            //HelperModule.Instance.SetLabel(i.ToString(), mesh.sharedMesh.bounds.center, i.ToString() + "|" + distance.ToString("0.00"));
        }

        //HelperModule.Instance.SetInfo("VertexCount", vertex_count.ToString());
        //HelperModule.Instance.SetInfo("TriangleCount", triangle_count.ToString());
        //HelperModule.Instance.SetInfo("BoundsMin", min_pos.ToString());
        //HelperModule.Instance.SetInfo("BoundsMax", max_pos.ToString());
        //HelperModule.Instance.SetInfo("Center", ((min_pos + max_pos) * 0.5f).ToString());
    }

    void OnDestroy()
    {
        ReleaseBuffer(ref bufferVertex);

        ReleaseBuffer(ref bufferNormal);
    }
 }
