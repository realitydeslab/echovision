using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class HumanStencilTransformTests
{
    [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
    public void Stencil_UsesRenderedCameraTransform_WithCropAndRotation(int angle)
    {
        var root = new GameObject("Stencil transform test"); root.SetActive(false);
        var background = root.AddComponent<ARCameraBackground>();
        var shader = Shader.Find("Unlit/ARKitBackground");
        Assert.That(shader, Is.Not.Null);
        var material = new Material(shader);
        background.customMaterial = material; background.useCustomMaterial = true;
        var depth = root.AddComponent(Type.GetType("DepthImageProcessor, Assembly-CSharp", true));
        var type = depth.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetField("background",flags).SetValue(depth,background);
        try
        {
            // A stale provider frame must not override the rendered camera's correction.
            type.GetMethod("OnCameraFrameEventReceived",flags).Invoke(depth,
                new object[] {new ARCameraFrameEventArgs {displayMatrix=Matrix4x4.identity}});
            Assert.That(type.GetProperty("DisplayRotatioMatrix").GetValue(depth), Is.EqualTo(Matrix4x4.identity),
                "Before the first camera frame, a material without a matrix must use the fallback without logging errors");
            foreach (float crop in new[] {.56f,.75f}) // two capture aspect ratios
            {
                float a=angle*Mathf.Deg2Rad, c=Mathf.Cos(a), s=Mathf.Sin(a);
                var camera=Matrix4x4.identity;
                camera.m00=c*crop; camera.m10=-s*crop;
                camera.m01=s; camera.m11=c;
                camera.m20=.5f-.5f*(camera.m00+camera.m10);
                camera.m21=.5f-.5f*(camera.m01+camera.m11);
                material.SetMatrix("_UnityDisplayTransform",camera);
                var actual=(Matrix4x4)type.GetProperty("DisplayRotatioMatrix").GetValue(depth);
                // Probe centre and corners; catches removed scale/translation and wrong orientation.
                foreach(var uv in new[] {Vector2.zero,Vector2.one,new Vector2(.2f,.8f),new Vector2(.5f,.5f)})
                {
                    var expected=new Vector2(.5f+crop*(c*(uv.x-.5f)-s*(uv.y-.5f)),
                        .5f+s*(uv.x-.5f)+c*(uv.y-.5f));
                    var sampled=new Vector2(uv.x*actual.m00+uv.y*actual.m10+actual.m20,
                        uv.x*actual.m01+uv.y*actual.m11+actual.m21);
                    Assert.That(Vector2.Distance(expected,sampled),Is.LessThan(.00001f));
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(material); }
    }
}
