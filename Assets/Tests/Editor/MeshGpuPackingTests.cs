using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class MeshGpuPackingTests
{
    struct Vertex { public Vector3 position, normal; public Vector2 uv; }

    [TestCase(false)] [TestCase(true)]
    public void GpuPacking_RespectsStreamsTransformsAndDestinationBounds(bool separateStreams)
    {
        Assert.That(SystemInfo.supportsComputeShaders, Is.True, "This check must run on a GPU");
        var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/VFX/MeshPointCache.compute");
        Assert.That(shader, Is.Not.Null);
        var root = new GameObject("GPU packing test"); root.SetActive(false);
        var component = root.AddComponent(Type.GetType("MeshVFX, Assembly-CSharp", true));
        var type = component.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var mesh = new Mesh();
        var points = new[] { new Vector3(1,2,3), new Vector3(-2,1,4), Vector3.one };
        var normals = new[] { new Vector3(1,1,0).normalized, Vector3.forward, Vector3.up };
        if (separateStreams)
        {
            mesh.SetVertexBufferParams(3, new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3,0),
                new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3,1));
            mesh.SetVertexBufferData(points,0,0,3,0); mesh.SetVertexBufferData(normals,0,0,3,1);
        }
        else
        {
            mesh.SetVertexBufferParams(3, new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2));
            var interleaved = new Vertex[3];
            for(int i=0;i<3;i++) interleaved[i] = new Vertex { position=points[i], normal=normals[i] };
            mesh.SetVertexBufferData(interleaved,0,0,3);
        }
        using var output = new GraphicsBuffer(GraphicsBuffer.Target.Structured,4,12);
        using var normalOutput = new GraphicsBuffer(GraphicsBuffer.Target.Structured,4,12);
        var sentinel = new Vector3(-99,-99,-99);
        output.SetData(new[] {sentinel,sentinel,sentinel,sentinel});
        normalOutput.SetData(new[] {sentinel,sentinel,sentinel,sentinel});
        type.GetField("meshPacking",flags).SetValue(component,shader);
        type.GetField("vertexBuffer",flags).SetValue(component,output);
        type.GetField("normalBuffer",flags).SetValue(component,normalOutput);
        try
        {
            var transform = Matrix4x4.TRS(new Vector3(4,5,6),Quaternion.Euler(17,31,23),new Vector3(2,3,4));
            Assert.That(type.GetMethod("PackMesh",flags).Invoke(component,new object[] {mesh,transform,1,2}),Is.True);
            // Test-only readback verifies the actual compute output, including nonuniform-scale normals.
            var actual = new Vector3[4]; var actualNormals = new Vector3[4];
            output.GetData(actual); normalOutput.GetData(actualNormals);
            Assert.That(actual[0],Is.EqualTo(sentinel)); Assert.That(actual[3],Is.EqualTo(sentinel));
            for(int i=0;i<2;i++)
            {
                Assert.That(Vector3.Distance(actual[i+1],transform.MultiplyPoint3x4(points[i])),Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(actualNormals[i+1],transform.inverse.transpose.MultiplyVector(normals[i]).normalized),Is.LessThan(.0001f));
            }
        }
        finally
        {
            type.GetField("vertexBuffer",flags).SetValue(component,null);
            type.GetField("normalBuffer",flags).SetValue(component,null);
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
}
