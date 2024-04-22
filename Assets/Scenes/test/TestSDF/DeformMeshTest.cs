using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//[ExecuteAlways]
public class DeformMeshTest : MonoBehaviour
{
    public Mesh mesh;
    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
    }

    void Update()
    {
        if(mesh == null)
        {
            mesh = GetComponent<MeshFilter>().sharedMesh;
        }
        Vector3 dir = Vector3.zero ;
        for(int i=0; i<mesh.vertices.Length; i++)
        {
            dir = mesh.vertices[i].normalized;
            mesh.vertices[i] = new Vector3(dir.x * transform.localScale.x, dir.y * transform.localScale.y, dir.z * transform.localScale.z);
        }
    }
}
