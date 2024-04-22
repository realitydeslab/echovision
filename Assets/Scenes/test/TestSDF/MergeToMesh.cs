using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class MergeToMesh : MonoBehaviour
{

    ARMeshManager m_MeshManager = null;

    private List<Vector3> data;

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


    List<Color> preparedColorList = new List<Color>();
    Color[] colors = new Color[8];
    List<Color> colorList = new List<Color>();


    SDFTexture sdfTexture;
    void Start()
    {
        m_MeshManager = FindObjectOfType<ARMeshManager>();
        combinedMesh = GetComponent<MeshFilter>().mesh;
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        sdfTexture = FindObjectOfType<SDFTexture>();

        //string msg = "Vertex:";
        //for(int i=0; i< combinedMesh.vertices.Length; i++)
        //{
        //    msg += combinedMesh.vertices[i].ToString() + " | ";
        //}
        //Debug.Log(msg);


        //msg = "Triangles:";
        //for (int i = 0; i < combinedMesh.triangles.Length; i++)
        //{

        //        msg += combinedMesh.triangles[i].ToString();
        //    msg += (i + 1) % 3 == 0 ? " | " : ", ";
        //}
        //Debug.Log(msg);

        for(int i=0; i<32; i++)
        {
            preparedColorList.Add(Random.ColorHSV());
        }
    }


    void Update()
    {

        IList<MeshFilter> mesh_list = m_MeshManager.meshes;

        if (mesh_list != null)
        {
            Debug.Log(mesh_list.Count);
            int mesh_count = mesh_list.Count;
            int vertex_count = 0;
            Vector3 min_pos = Vector3.zero;
            Vector3 max_pos = Vector3.zero;

            
            combinedMesh.Clear();
            
            vertexList.Clear();
            indexList.Clear();

            /*

            for (int i= 0; i < mesh_list.Count; i++)
            {
                vertex_count += mesh_list[i].sharedMesh.vertexCount;


                
                Bounds bounds = mesh_list[i].sharedMesh.bounds;
                min_pos = Vector3.Min(min_pos, bounds.min);
                max_pos = Vector3.Max(max_pos, bounds.max);
          
                for (int k =0;k<8; k++)
                {
                    vertices[k] = Vector3.Scale(originalVertices[k], bounds.extents) + bounds.center;
                    //colors[k] = colorList[i % colorList.Count];
                }
                Debug.Log(vertices);
                
                for (int k=0; k<36;k++)
                {
                    indices[k] = originalIndices[k] + i * 8;
                }
                Debug.Log(indices);

                vertexList.AddRange(vertices);
                indexList.AddRange(indices);
                //colorList.AddRange(colors);

                combinedMesh.SetVertices(vertexList);
                combinedMesh.SetTriangles(indexList, 0);
                //combinedMesh.SetColors(colorList);
                //combinedMesh.RecalculateBounds();
                //combinedMesh.RecalculateNormals();
                
            }

            */

            int i = 0;
            CombineInstance[] combine = new CombineInstance[mesh_list.Count];
            while (i < mesh_list.Count)
            {
                combine[i].mesh = mesh_list[i].sharedMesh;
                combine[i].transform = mesh_list[i].transform.localToWorldMatrix;
                mesh_list[i].gameObject.SetActive(false);

                i++;
            }

            combinedMesh.CombineMeshes(combine);
            //transform.GetComponent<MeshFilter>().sharedMesh = combinedMesh;

            //Mesh mesh = new Mesh();
            //mesh.CombineMeshes(combine);
            //transform.GetComponent<MeshFilter>().sharedMesh = mesh;
            //transform.gameObject.SetActive(true);
        }
    }

    public void OnChangeDensity(float v)
    {
        m_MeshManager.density = v;
    }
}
