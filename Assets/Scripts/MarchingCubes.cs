using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static MarchingCubes.MarchingTable;

namespace MarchingCubes
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MarchingCubes : MonoBehaviour
    {
        public int cubePerAxis = 32; // 每个轴上的体素数量
        public float isoLevel = 0.8f; // 等值面,即生成的网格表面密度值
        [Header("Noise Settings")]
        public float noiseScale = 0.1f; // 噪声缩放
        public int octaves = 8; // 八度
        public float persistence = 0.5f; // 持久度
        public float lacunarity = 1.6f; // 分形维度
        public bool visualizeNoise = false; // 是否可视化噪声
        public bool isPlain = false; // 是否为平面

        private List<Vector3> vertices = new List<Vector3>(); // 顶点列表
        private List<int> triangles = new List<int>(); // 三角形列表
        private float[,,] density; // 密度函数

        private MeshFilter meshFilter;
        private Mesh mesh;

        void Start()
        {
            meshFilter = GetComponent<MeshFilter>();
            StartCoroutine(GenerateMesh());
        }

        // 通过协程实时生成网格
        private IEnumerator GenerateMesh()
        {
            SetDensity(); // 设置密度函数
            MarchingAllCubes(); // 遍历体素
            SetMesh(); // 生成网格
            
            while (true)
            {
                if (Input.GetKeyDown(KeyCode.G))
                {
                    SetDensity(); // 设置密度函数
                    MarchingAllCubes(); // 遍历体素
                    SetMesh(); // 生成网格
                }
                yield return null;
            }
        }

        // 设置密度函数
        private void SetDensity()
        {
            density = new float[cubePerAxis + 1, cubePerAxis + 1, cubePerAxis + 1]; // 创建密度函数

            // 三层嵌套循环遍历
            for (int x = 0; x <= cubePerAxis; x++)
            {
                for (int y = 0; y <= cubePerAxis; y++)
                {
                    for (int z = 0; z <= cubePerAxis; z++)
                    {
                        if (isPlain){
                            float currentDensity = cubePerAxis * Mathf.PerlinNoise(x * noiseScale * 0.3f, z * noiseScale * 0.3f); // 二维噪声
                            float distToSurface;

                            if (y <= currentDensity - isoLevel)
                                distToSurface = 0f;
                            else if (y >= currentDensity + isoLevel)
                                distToSurface = 1f;
                            else if (y < currentDensity)
                                distToSurface = y - currentDensity;
                            else
                                distToSurface = currentDensity - y;

                            density[x, y, z] = distToSurface; // 平面密度
                        }
                        else 
                            density[x, y, z] = NoiseFunction(x, y, z); // 三维噪声
                        
                        Debug.Log("密度值：" + density[x, y, z]);
                    }
                }
            }
        }

        // 生成网格
        private void SetMesh()
        {
            mesh = new Mesh(); // 创建网格

            mesh.vertices = vertices.ToArray(); // 顶点
            mesh.triangles = triangles.ToArray(); // 三角形
            mesh.RecalculateNormals(); // 重新计算法线

            meshFilter.mesh = mesh; // 设置网格
        }

#region Noise
        // 噪声函数
        public float NoiseFunction(int x, int y, int z)
        {
            float noiseValue = 0.0f; // 初始噪声值
            float frequency = 0.5f; // 初始频率
            float amplitude = 1.0f; // 初始振幅

            // 循环添加多个不同频率和振幅的Perlin噪声
            for (int i = 0; i < octaves; i++)
            {
                float xCoord = x * frequency * noiseScale;
                float yCoord = y * frequency * noiseScale;
                float zCoord = z * frequency * noiseScale;

                noiseValue += SetNoise(xCoord, yCoord, zCoord) * amplitude;

                // 更新频率和振幅
                frequency *= lacunarity;
                amplitude *= persistence;
            }

            return noiseValue;
        }

        public float SetNoise(float x, float y, float z)
        {
            float xy = Mathf.PerlinNoise(x, y);
            float xz = Mathf.PerlinNoise(x, z);
            float yz = Mathf.PerlinNoise(y, z);

            return (xy + xz + yz) / 3f; // 取平均值
        }
#endregion

        // 遍历体素
        private void MarchingAllCubes()
        {
            vertices.Clear(); // 清空顶点列表
            triangles.Clear(); // 清空三角形列表

            // 三层嵌套循环遍历
            for (int x = 0; x < cubePerAxis; x++)
            {
                for (int y = 0; y < cubePerAxis; y++)
                {
                    for (int z = 0; z < cubePerAxis; z++)
                    {
                        MarchingCube(x, y, z);
                    }
                }
            }
        }

        // 为每个体素查找相应索引
        private void MarchingCube(int x, int y, int z)
        {
            // 获取当前体素八个顶点
            float[] cubeCorners = {
                density[x, y, z],
                density[x + 1, y, z],
                density[x + 1, y, z + 1],
                density[x, y, z + 1],
                density[x, y + 1, z],
                density[x + 1, y + 1, z],
                density[x + 1, y + 1, z + 1],
                density[x, y + 1, z + 1]
            };

            int cubeIndex = GetCubeIndex(cubeCorners); // 获取体素索引

            // 根据索引查表获取三角形配置
            for (int i = 0; MarchingTable.triangulation[cubeIndex, i] != -1; i += 3)
            {
                int a0 = MarchingTable.cornerIndexAFromEdge[MarchingTable.triangulation[cubeIndex, i]];
                int b0 = MarchingTable.cornerIndexBFromEdge[MarchingTable.triangulation[cubeIndex, i]];

                int a1 = MarchingTable.cornerIndexAFromEdge[MarchingTable.triangulation[cubeIndex, i + 1]];
                int b1 = MarchingTable.cornerIndexBFromEdge[MarchingTable.triangulation[cubeIndex, i + 1]];

                int a2 = MarchingTable.cornerIndexAFromEdge[MarchingTable.triangulation[cubeIndex, i + 2]];
                int b2 = MarchingTable.cornerIndexBFromEdge[MarchingTable.triangulation[cubeIndex, i + 2]];

                // 插值计算三角形顶点
                Vector3 v0 = Interpolate(cubeCorners[a0], cubeCorners[b0], GetCoord(x, y, z, a0), GetCoord(x, y, z, b0));
                Vector3 v1 = Interpolate(cubeCorners[a1], cubeCorners[b1], GetCoord(x, y, z, a1), GetCoord(x, y, z, b1));
                Vector3 v2 = Interpolate(cubeCorners[a2], cubeCorners[b2], GetCoord(x, y, z, a2), GetCoord(x, y, z, b2));

                // 添加三角形顶点
                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);

                // 添加三角形
                triangles.Add(vertices.Count - 3);
                triangles.Add(vertices.Count - 2);
                triangles.Add(vertices.Count - 1);
            }
        }

        // 获取体素索引
        private int GetCubeIndex(float[] cubeCorners)
        {
            int cubeIndex = 0; // 体素索引

            for (int i = 0; i < 8; i++)
            {
                if (cubeCorners[i] < isoLevel)
                {
                    cubeIndex |= 1 << i; // 按位或运算
                }
            }

            return cubeIndex;
        }

        // 获取顶点坐标
        private Vector3 GetCoord(int x, int y, int z, int index)
        {
            switch (index)
            {
                case 0:
                    return new Vector3(x, y, z);
                case 1:
                    return new Vector3(x + 1, y, z);
                case 2:
                    return new Vector3(x + 1, y, z + 1);
                case 3:
                    return new Vector3(x, y, z + 1);
                case 4:
                    return new Vector3(x, y + 1, z);
                case 5:
                    return new Vector3(x + 1, y + 1, z);
                case 6:
                    return new Vector3(x + 1, y + 1, z + 1);
                case 7:
                    return new Vector3(x, y + 1, z + 1);
                default:
                    return Vector3.zero;
            }
        }

        // 插值计算三角形顶点
        private Vector3 Interpolate(float a, float b, Vector3 vertexA, Vector3 vertexB)
        {
            float t = (isoLevel - a) / (b - a); // 插值比例

            // 插值计算顶点坐标
            Vector3 vertex = vertexA + t * (vertexB - vertexA);
            return vertex;
        }

        // 可视化噪声
        private void OnDrawGizmos()
        {
            if (visualizeNoise)
            {
                for (int x = 0; x < cubePerAxis; x++)
                {
                    for (int y = 0; y < cubePerAxis; y++)
                    {
                        for (int z = 0; z < cubePerAxis; z++)
                        {
                            Gizmos.color = new Color(density[x, y, z], density[x, y, z], density[x, y, z], 1);
                            Gizmos.DrawSphere(new Vector3(x, y, z), 0.2f);
                        }
                    }
                }
            }
        }

    }
}
