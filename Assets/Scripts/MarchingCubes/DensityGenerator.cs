using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DensityGenerator : MonoBehaviour {

    const int threadGroupSize = 8;
    public ComputeShader densityShader;

    [Header("噪声设置")]
    public int seed; // 种子
    public int numOctaves = 4;
    public float lacunarity = 2;
    public float persistence = 0.5f;
    public float noiseScale = 1;
    public float noiseWeight = 1;
    public bool closeEdges;
    public float floorOffset = 1;
    public float weightMultiplier = 1;
    public float hardFloorHeight; // 地形基底
    public float hardFloorWeight;
    public Vector4 shaderParams;

    List<ComputeBuffer> buffersToRelease;

    void OnValidate() {
        if (FindObjectOfType<MeshGenerator>()) {
            FindObjectOfType<MeshGenerator>().MeshUpdate();
        }
    }

    public ComputeBuffer Generate (ComputeBuffer pointsBuffer, int numPointsPerAxis, float boundsSize, Vector3 worldBounds, Vector3 centre, Vector3 offset, float spacing) 
    {
        // 噪声参数
        var prng = new System.Random (seed); // 伪随机数生成器
        var offsets = new Vector3[numOctaves]; // 偏移量
        float offsetRange = 1000;
        for (int i = 0; i < numOctaves; i++) {
            offsets[i] = new Vector3 ((float) prng.NextDouble () * 2 - 1, (float) prng.NextDouble () * 2 - 1, (float) prng.NextDouble () * 2 - 1) * offsetRange;
        } // 随机生成偏移量

        // 偏移量缓冲区
        var offsetsBuffer = new ComputeBuffer (offsets.Length, sizeof (float) * 3);
        offsetsBuffer.SetData (offsets);
        buffersToRelease.Add (offsetsBuffer);

        // 设置噪声参数
        densityShader.SetVector ("centre", new Vector4 (centre.x, centre.y, centre.z));
        densityShader.SetInt ("octaves", Mathf.Max (1, numOctaves));
        densityShader.SetFloat ("lacunarity", lacunarity);
        densityShader.SetFloat ("persistence", persistence);
        densityShader.SetFloat ("noiseScale", noiseScale);
        densityShader.SetFloat ("noiseWeight", noiseWeight);
        densityShader.SetBool ("closeEdges", closeEdges);
        densityShader.SetBuffer (0, "offsets", offsetsBuffer);
        densityShader.SetFloat ("floorOffset", floorOffset);
        densityShader.SetFloat ("weightMultiplier", weightMultiplier);
        densityShader.SetFloat ("hardFloor", hardFloorHeight);
        densityShader.SetFloat ("hardFloorWeight", hardFloorWeight);
        densityShader.SetVector ("params", shaderParams);

        // 设置网格参数
        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        int numThreadsPerAxis = Mathf.CeilToInt (numPointsPerAxis / (float) threadGroupSize);
        densityShader.SetBuffer (0, "points", pointsBuffer);
        densityShader.SetInt ("numPointsPerAxis", numPointsPerAxis);
        densityShader.SetFloat ("boundsSize", boundsSize);
        densityShader.SetVector ("centre", new Vector4 (centre.x, centre.y, centre.z));
        densityShader.SetVector ("offset", new Vector4 (offset.x, offset.y, offset.z));
        densityShader.SetFloat ("spacing", spacing);
        densityShader.SetVector("worldSize", worldBounds);

        // 执行密度核函数
        densityShader.Dispatch (0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

        // 释放缓冲区
        if (buffersToRelease != null) {
            foreach (var b in buffersToRelease) {
                b.Release ();
            }
        }

        // 返回点缓冲区
        return pointsBuffer;
    }
}

