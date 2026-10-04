// SPDX-FileCopyrightText: Copyright 2024-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using UnityEngine.XR.ARFoundation;

public class MeshVFX : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] ARMeshManager meshManager;
    [SerializeField] TrackedPoseDriver trackedPoseDriver;
    [SerializeField] VisualEffect vfx;
    [SerializeField] ComputeShader meshPacking;
    [Header("Buffer Settings")]
    [SerializeField] int bufferInitialCapacity = 64000;
    [SerializeField] bool dynamicallyResizeBuffer = false;

    const int BufferStride = 12;
    static readonly VertexAttribute[] RequiredAttributes = { VertexAttribute.Position, VertexAttribute.Normal };
    static readonly ProfilerMarker UploadMarker = new("EchoVision.MeshVFX.GPUPack");
    readonly List<(float distance, int index)> meshOrder = new();
    GraphicsBuffer vertexBuffer, normalBuffer;
    bool dirty = true;
    Vector3 lastHeadPosition;

    void Awake()
    {
        if (!SystemInfo.supportsComputeShaders || meshPacking == null)
        {
            Debug.LogError("[MeshVFX] GPU mesh packing requires a compute shader and a supported GPU.");
            enabled = false; return;
        }
        EnsureBuffer(ref vertexBuffer, bufferInitialCapacity, "MeshPointCache");
        EnsureBuffer(ref normalBuffer, bufferInitialCapacity, "MeshNormalCache");
    }

    void OnEnable()
    {
        dirty = true;
        meshManager.meshInfosChanged.AddListener(OnMeshesChanged);
    }

    void OnDisable() => meshManager.meshInfosChanged.RemoveListener(OnMeshesChanged);
    void OnMeshesChanged(ARMeshInfosChangedEventArgs _) => dirty = true;

    void LateUpdate()
    {
        var meshes = meshManager.meshes;
        if (meshes == null) return;
        // Origin changes must still reach the VFX even when geometry is unchanged.
        Transform meshTransform = meshes.Count > 0 ? meshes[0].transform : null;
        vfx.SetVector3("MeshTransform_position", meshTransform != null ? meshTransform.position : Vector3.zero);
        vfx.SetVector3("MeshTransform_angles", meshTransform != null ? meshTransform.eulerAngles : Vector3.zero);
        vfx.SetVector3("MeshTransform_scale", meshTransform != null ? meshTransform.localScale : Vector3.one);

        Vector3 head = trackedPoseDriver.transform.position;
        // Re-select the nearest points after 5 cm of movement; particles still render every frame.
        if (!dirty && (head-lastHeadPosition).sqrMagnitude < .0025f) return;
        using var sample = UploadMarker.Auto();
        dirty = false; lastHeadPosition = head;
        meshOrder.Clear();
        int total = 0;
        for (int i=0; i<meshes.Count; i++)
        {
            var filter = meshes[i];
            if (filter == null || filter.sharedMesh == null) continue;
            Vector3 center = filter.transform.TransformPoint(filter.sharedMesh.bounds.center);
            meshOrder.Add(((head-center).sqrMagnitude, i));
            total += filter.sharedMesh.vertexCount;
        }
        if (dynamicallyResizeBuffer)
        {
            for (int i=meshOrder.Count-1; i>0; i--)
            {
                int j=Random.Range(0,i+1);
                (meshOrder[i],meshOrder[j])=(meshOrder[j],meshOrder[i]);
            }
        }
        else meshOrder.Sort((a,b) => a.distance.CompareTo(b.distance));

        int count = dynamicallyResizeBuffer ? total : Mathf.Min(total, bufferInitialCapacity);
        EnsureBuffer(ref vertexBuffer, count, "MeshPointCache");
        EnsureBuffer(ref normalBuffer, count, "MeshNormalCache");
        int offset = 0;
        foreach (var entry in meshOrder)
        {
            var filter = meshes[entry.index];
            int length = Mathf.Min(filter.sharedMesh.vertexCount, count-offset);
            if (length <= 0) break;
            Matrix4x4 toReference = meshTransform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            if (!PackMesh(filter.sharedMesh, toReference, offset, length))
            {
                vfx.SetInt("MeshPointCount", 0); enabled = false; return;
            }
            offset += length;
        }
        vfx.SetInt("MeshPointCount", offset);
    }

    bool PackMesh(Mesh mesh, Matrix4x4 toReference, int offset, int count)
    {
        // ARKit supplies float3 position/normal streams. Never read mesh arrays back to the CPU.
        foreach (var attribute in RequiredAttributes)
        {
            if (!mesh.HasVertexAttribute(attribute) || mesh.GetVertexAttributeFormat(attribute) != VertexAttributeFormat.Float32
                || mesh.GetVertexAttributeDimension(attribute) < 3)
            {
                Debug.LogError($"[MeshVFX] Unsupported GPU mesh attribute: {attribute}");
                return false;
            }
        }
        if ((mesh.vertexBufferTarget & GraphicsBuffer.Target.Raw) == 0)
            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        int positionStream = mesh.GetVertexAttributeStream(VertexAttribute.Position);
        int normalStream = mesh.GetVertexAttributeStream(VertexAttribute.Normal);
        // Mesh updates can replace its buffers; acquire fresh handles for each pack.
        using var positions = mesh.GetVertexBuffer(positionStream);
        using var normals = mesh.GetVertexBuffer(normalStream);
        meshPacking.SetBuffer(0, "SourcePositions", positions);
        meshPacking.SetBuffer(0, "SourceNormals", normals);
        meshPacking.SetBuffer(0, "Positions", vertexBuffer);
        meshPacking.SetBuffer(0, "Normals", normalBuffer);
        meshPacking.SetInt("PositionStride", mesh.GetVertexBufferStride(positionStream));
        meshPacking.SetInt("NormalStride", mesh.GetVertexBufferStride(normalStream));
        meshPacking.SetInt("PositionOffset", mesh.GetVertexAttributeOffset(VertexAttribute.Position));
        meshPacking.SetInt("NormalOffset", mesh.GetVertexAttributeOffset(VertexAttribute.Normal));
        meshPacking.SetInt("DestinationOffset", offset);
        meshPacking.SetInt("VertexCount", count);
        meshPacking.SetMatrix("ToReference", toReference);
        meshPacking.SetMatrix("NormalToReference", toReference.inverse.transpose);
        meshPacking.Dispatch(0, (count+63)/64, 1, 1);
        return true;
    }

    void EnsureBuffer(ref GraphicsBuffer buffer, int capacity, string property)
    {
        capacity = Mathf.Max(1, capacity);
        if (buffer != null && buffer.count >= capacity) return;
        buffer?.Release();
        buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, BufferStride);
        vfx.SetGraphicsBuffer(property, buffer);
    }

    void OnDestroy()
    {
        vertexBuffer?.Release(); normalBuffer?.Release();
    }
}
