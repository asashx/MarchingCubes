using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public Vector3Int coord;

    public Mesh mesh;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;

    public void Destroy()
    {
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject, false);
        }
    }

    public void SetUp(Material material)
    {
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (meshFilter == null) meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = gameObject.GetComponent<MeshRenderer>();

        mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.sharedMesh = mesh;
        }

        meshRenderer.material = material;
    }
}
