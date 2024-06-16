using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARFoundation.Samples;

public class DrawBoundingBox : MonoBehaviour
{
    public HumanBodyTracker humanBodyTracker;
    public bool bodyFilter = true;
    ARMeshManager m_MeshManager = null;
    Mesh combinedMesh;

    List<Vector3> vertexList = new List<Vector3>();
    List<int> indexList = new List<int>();


    Vector3[] originalVertices = new Vector3[]{
        new Vector3(-1, -1, -1), //0
        new Vector3( 1, -1, -1), //1
        new Vector3(-1,  1, -1), //2
        new Vector3( 1,  1, -1), //3
        new Vector3(-1, -1,  1), //4
        new Vector3( 1, -1,  1), //5
        new Vector3(-1,  1,  1), //6
        new Vector3( 1,  1,  1)  //7
    };
    Vector3[] vertices = new Vector3[8];
    int[] originalIndices = new int[] {
        //Top
        2, 7, 6,
        2, 3, 7,

        //Bottom
        0, 5, 4,
        0, 1, 5,

        //Left
        0, 2, 6,
        0, 6, 4,

        //Right
        1, 7, 3,
        1, 5, 7,

        //Front
        0, 3, 2,
        0, 1, 3,

        //Back
        4, 7, 6,
        4, 5, 7};
    int[] indices = new int[36];


    enum SelectedJointIndices
    {
        Root = 0, // parent: <none> [-1]
        Hips = 1, // parent: Root [0]
        LeftLeg = 3, // parent: LeftUpLeg [2]
        LeftFoot = 4, // parent: LeftLeg [3]
        RightLeg = 8, // parent: RightUpLeg [7]
        RightFoot = 9, // parent: RightLeg [8]
        LeftShoulder1 = 19, // parent: Spine7 [18]
        LeftArm = 20, // parent: LeftShoulder1 [19]
        LeftForearm = 21, // parent: LeftArm [20]
        LeftHand = 22, // parent: LeftForearm [21]
        Neck1 = 47, // parent: Spine7 [18]
        Head = 51, // parent: Neck4 [50]
        RightShoulder1 = 63, // parent: Spine7 [18]
        RightArm = 64, // parent: RightShoulder1 [63]
        RightForearm = 65, // parent: RightArm [64]
        RightHand = 66, // parent: RightForearm [65]
    }
    const int selectedJointCount = 16;
    List<Vector3> jointPositionList = new List<Vector3>();

    // Start is called before the first frame update
    void Start()
    {
        m_MeshManager = FindObjectOfType<ARMeshManager>();
        combinedMesh = GetComponent<MeshFilter>().mesh;
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    }

    // Update is called once per frame
    void Update()
    {
        HelperModule.Instance.SetInfo("BodyCount:", humanBodyTracker.m_SkeletonTracker.Count.ToString());
        HelperModule.Instance.SetInfo("MeshCount:", m_MeshManager.meshes.Count.ToString());


        IList<MeshFilter> mesh_list = m_MeshManager.meshes;
        int body_inside_count = 0;
        bool body_inside = false;

        if (mesh_list != null)
        {
            int vertex_count = 0;
            Vector3 min_pos = Vector3.zero;
            Vector3 max_pos = Vector3.zero;

            combinedMesh.Clear();

            vertexList.Clear();
            indexList.Clear();

            for (int i= 0; i < mesh_list.Count; i++)
            {
                //if(bodyFilter)
                //{
                    body_inside = false;
                    jointPositionList.Clear();
                    foreach (BoneController bone in humanBodyTracker.m_SkeletonTracker.Values)
                    {
                        foreach (int k in Enum.GetValues(typeof(SelectedJointIndices)))
                        {
                            jointPositionList.Add(bone.m_BoneMapping[k].position);
                            if(mesh_list[i].sharedMesh.bounds.Contains(bone.m_BoneMapping[k].position))
                            {
                                body_inside = true;
                                break;
                            }
                        }

                        if (body_inside)
                            break;
                    }

                    if(body_inside)
                        body_inside_count++;
                //}

                if (bodyFilter && body_inside == false)
                    continue;

                vertex_count += mesh_list[i].sharedMesh.vertexCount;
                
                Bounds bounds = mesh_list[i].sharedMesh.bounds;
                min_pos = Vector3.Min(min_pos, bounds.min);
                max_pos = Vector3.Max(max_pos, bounds.max);
          
                for (int k =0;k<8; k++)
                {
                    vertices[k] = Vector3.Scale(originalVertices[k], bounds.extents) + bounds.center;
                }
                
                for (int k=0; k<36;k++)
                {
                    indices[k] = originalIndices[k] + i * 8;
                }

                vertexList.AddRange(vertices);
                indexList.AddRange(indices);
            }

            HelperModule.Instance.SetInfo("MeshCount_ContainBody:", body_inside_count.ToString());

            combinedMesh.SetVertices(vertexList);
            combinedMesh.SetTriangles(indexList, 0);

            combinedMesh.RecalculateBounds();
            combinedMesh.RecalculateNormals();           
        }
    }
}
