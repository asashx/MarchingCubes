using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class MeshGenrator : MonoBehaviour
{
    const int threadGroupSize = 8; // 线程组大小

    [Header("基础设置")]
    public DensityGenerator densityGenerator; // 密度函数
    public Vector3Int numChunks = Vector3Int.one; // 分区块数量
    public ComputeShader shader; // MC计算着色器
    public Material material; // 材质

    [Header("体素设置")]
    public float isoLevel = 0; // 等值面
    public float boundsSize = 20; // 边界大小
    public Vector3 offset = Vector3.zero; // 偏移量

    [Range (2, 100)]
    public int numPointsPerAxis = 50; // 每个轴上的点数,控制精细度

    // 分块相关
    GameObject chunkHolder;
    const string chunkHolderName = "Chunk Holder"; // 分块持有者名称
    List<Chunk> chunks; // 分块列表

    // 缓冲区
    ComputeBuffer triangleBuffer; // 三角形缓冲区
    ComputeBuffer pointBuffer; // 点缓冲区
    ComputeBuffer triangleCountBuffer; // 三角形计数缓冲区

    bool settingsUpdated; // 设置是否更新

#region Runtime
    void Awake()
    {
        if (Application.isPlaying)
        {
            InitChunkStrutures(); // 初始化分块结构

            // 销毁旧的分块
            var oldChunks = FindObjectsOfType<Chunk>(); 
            foreach (var oldChunk in oldChunks)
            {
                Destroy(oldChunk.gameObject);
            }
        }
    }

    void Update()
    {
        // 在运行时运行
        if (Application.isPlaying)
        {
            Run();
        }

        // 设置更新时更新网格
        if (settingsUpdated)
        {
            settingsUpdated = false;
            MeshUpdate ();
        }
    }

    public void Run ()
    {
        CreateBuffers(); // 创建缓冲区

        InitChunks ();
        UpdateAllChunks ();

        if (!Application.isPlaying)
        {
            DisposeBuffers(); // 销毁缓冲区
        }
    }

    public void MeshUpdate ()
    {
        if (!Application.isPlaying)
        {
            Run();
        }
    }

    void OnDestroy()
    {
        if (Application.isPlaying)
        {
            DisposeBuffers(); // 销毁缓冲区
        }
    }

    void OnValidate()
    {
        settingsUpdated = true;
    }
#endregion

#region Chunk Related
    void InitChunkStrutures()
    {
        // 初始化分块结构
        chunks = new List<Chunk>();
    }

    Vector3 CentreFromCoord(Vector3Int coord)
    {
        // 从坐标计算中心点
        Vector3 totalBounds = (Vector3) numChunks * boundsSize;
        return -totalBounds / 2 + (Vector3) coord * boundsSize + Vector3.one * boundsSize / 2;
    }

    void CreateChunkHolder()
    {
        // 创建分块持有者
        if (chunkHolder == null)
        {
            chunkHolder = GameObject.Find(chunkHolderName);
            if (chunkHolder == null)
            {
                chunkHolder = new GameObject(chunkHolderName);
            }
        }
    }

    void InitChunks()
    {
        CreateChunkHolder(); // 创建分块持有者
        chunks = new List<Chunk>();
        List<Chunk> oldChunks = new List<Chunk> (FindObjectsOfType<Chunk> ());

        // 遍历所有分块
        for (int x = 0; x < numChunks.x; x++)
        {
            for (int y = 0; y < numChunks.y; y++)
            {
                for (int z = 0; z < numChunks.z; z++)
                {
                    Vector3Int coord = new Vector3Int(x, y, z);
                    bool chunkExists = false;

                    // 如果分块已经存在
                    foreach (Chunk oldChunk in oldChunks)
                    {
                        if (oldChunk.coord == coord)
                        {
                            chunks.Add(oldChunk);
                            chunkExists = true;
                            oldChunks.Remove(oldChunk);
                            break;
                        }
                    }

                    // 如果分块不存在，生成
                    if (!chunkExists)
                    {
                        var newChunk = CreateChunk(coord);
                        chunks.Add(newChunk);
                    }

                    chunks[chunks.Count - 1].SetUp(material);
                }
            }
        }

        // 回收多余的分块
        foreach (Chunk oldChunk in oldChunks)
        {
            oldChunk.Destroy();
        }
    }

    Chunk CreateChunk (Vector3Int coord)
    {
        GameObject chunk = new GameObject ($"Chunk ({coord.x}, {coord.y}, {coord.z})");
        chunk.transform.parent = chunkHolder.transform;
        Chunk newchunk  = chunk.AddComponent<Chunk> ();
        newchunk.coord = coord;
        return newchunk;
    }

    public void UpdateAllChunks()
    {
        // 更新所有分块
        foreach (Chunk chunk in chunks)
        {
            UpdateChunkMesh(chunk);
        }
    }

    public void UpdateChunkMesh(Chunk chunk)
    {
        int numVoxelsPerAxis = numPointsPerAxis - 1; // 每个轴上的体素数
        int numThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerAxis / (float) threadGroupSize); // 每个轴上的线程数
        float pointSpacing = boundsSize / (numPointsPerAxis - 1); // 点间距

        Vector3Int coord = chunk.coord;
        Vector3 centre = CentreFromCoord(coord);

        Vector3 worldBounds = new Vector3(numChunks.x, numChunks.y, numChunks.z) * boundsSize;

        // 密度函数生成
        densityGenerator.Generate(pointBuffer, numPointsPerAxis, boundsSize, worldBounds, centre, offset, pointSpacing);

        // 设置计算着色器参数
        triangleBuffer.SetCounterValue(0);
        shader.SetBuffer(0, "poinsts", pointBuffer);
        shader.SetBuffer(0, "triangles", triangleBuffer);
        shader.SetInt("numPointsPerAxis", numPointsPerAxis);
        shader.SetFloat("isoLevel", isoLevel);

        // 调度计算着色器核函数
        shader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

        // 从缓冲区中获取三角形数量
        ComputeBuffer.CopyCount(triangleBuffer, triangleCountBuffer, 0);
        int[] triangleCountArray = { 0 };
        triangleCountBuffer.GetData(triangleCountArray);
        int numTris = triangleCountArray[0];

        // 从缓冲区中获取三角形
        Triangle[] tris = new Triangle[numTris];
        triangleBuffer.GetData(tris, 0, 0, numTris); // tris目标数组，0目标数组的起始索引，0源数组的起始索引，numTris要复制的元素数

        // 生成网格
        Mesh mesh = chunk.mesh;
        mesh.Clear();

        // 三角形数据和顶点数据
        var vertices = new Vector3[numTris * 3];
        var meshTriangles = new int[numTris * 3];

        for (int i = 0; i < numTris; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                meshTriangles[i * 3 + j] = i * 3 + j;
                vertices[i * 3 + j] = tris[i][j];
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = meshTriangles;
        mesh.RecalculateNormals();
    }

    struct Triangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public Vector3 this [int i]
        {
            get
            {
                switch (i)
                {
                    case 0:
                        return a;
                    case 1:
                        return b;
                    default:
                        return c;
                }
            }
        }
    }
#endregion

#region Buffers
    void CreateBuffers()
    {
        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis; // 点数
        int numVoxelsPerAxis = numPointsPerAxis - 1; // 每个轴上的体素数
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis; // 体素数
        int maxTriangleCount = numVoxels * 5; // 最大三角形数

        if (!Application.isPlaying || (pointBuffer == null || numPoints != pointBuffer.count))
        {
            if (Application.isPlaying)
            {
                DisposeBuffers(); // 销毁缓冲区
            }
            triangleBuffer = new ComputeBuffer (maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
            pointBuffer = new ComputeBuffer (numPoints, sizeof(float) * 4);
            triangleCountBuffer = new ComputeBuffer (1, sizeof(int), ComputeBufferType.Raw);
        }
    }

    void DisposeBuffers()
    {
        if (triangleBuffer != null)
        {
            triangleBuffer.Release();
            pointBuffer.Release();
            triangleCountBuffer.Release();
        }
    }
#endregion
}
