using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using UnityEditor;
using EasyRoads3Dv3;

public class ChunkedProceduralTerrain : MonoBehaviour
{
    [Header("Player & Chunk Settings")]
    public Transform player;
    public GameObject terrainPrefab;
    public int chunkSize = 128;
    public int renderDistance = 2;

    [Header("Terrain Noise Settings")]
    public int depth = 20;
    public float scale = 0.05f;
    public int octaves = 4;
    public float lacunarity = 2f;
    public float persistence = 0.5f;
    public float ridgeWeight = 1f;
    public Vector2 offset = Vector2.zero;

    [Header("Region Settings (Basins & Ridges)")]
    [Range(0.0005f, 0.01f)] public float regionScale = 0.002f;
    [Range(0f, 1f)] public float basinDepth = 0.3f;
    [Range(0f, 4f)] public float basinSharpness = 2f;

    private Dictionary<Vector2Int, Terrain> loadedChunks = new Dictionary<Vector2Int, Terrain>();
    private bool regenerate = false;



    void Start()
    {
        if (player == null || terrainPrefab == null)
        {
            Debug.LogError("Player or Terrain Prefab not assigned!");
            return;
        }

        StartCoroutine(UpdateChunks());
    }

    public void GenerateTerrain()
    {
        regenerate = true;

    }

    IEnumerator UpdateChunks()
    {
        while (true)
        {
            Vector2Int currentChunk = new Vector2Int(
                Mathf.FloorToInt(player.position.x / chunkSize),
                Mathf.FloorToInt(player.position.z / chunkSize)
            );

            for (int x = -renderDistance; x <= renderDistance; x++)
            {
                for (int z = -renderDistance; z <= renderDistance; z++)
                {
                    Vector2Int chunkCoord = currentChunk + new Vector2Int(x, z);
                    if (!loadedChunks.ContainsKey(chunkCoord))
                    {
                        yield return LoadChunk(chunkCoord);
                        yield return null;
                    }
                }
            }

            List<Vector2Int> chunksToRemove = new List<Vector2Int>();
            foreach (var kvp in loadedChunks)
            {
                if (regenerate || Mathf.Abs(kvp.Key.x - currentChunk.x) > renderDistance ||
                    Mathf.Abs(kvp.Key.y - currentChunk.y) > renderDistance)
                {
                    chunksToRemove.Add(kvp.Key);
                }
            }
            regenerate = false;
            foreach (var chunk in chunksToRemove)
            {
                UnloadChunk(chunk);
            }

            yield return new WaitForSeconds(0.25f); //Probably a better way to make this work 
        }
    }

    IEnumerator LoadChunk(Vector2Int coord)
    {
        GameObject chunkGO = Instantiate(terrainPrefab);
        chunkGO.name = $"Chunk_{coord.x}_{coord.y}";
        chunkGO.transform.position = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);

        Terrain chunkTerrain = chunkGO.GetComponent<Terrain>();
        TerrainData newData = Instantiate(terrainPrefab.GetComponent<Terrain>().terrainData);
        newData.heightmapResolution = chunkSize + 1;
        newData.size = new Vector3(chunkSize, depth, chunkSize);
        newData.terrainLayers = terrainPrefab.GetComponent<Terrain>().terrainData.terrainLayers;

        chunkTerrain.terrainData = newData;

        TerrainCollider collider = chunkGO.GetComponent<TerrainCollider>();
        if (collider != null)
            collider.terrainData = newData;

        int res = chunkSize + 1;
        NativeArray<float> heights1D = new NativeArray<float>(res * res, Allocator.TempJob);

        PerlinJob job = new PerlinJob
        {
            res = res,
            worldX = coord.x * chunkSize,
            worldZ = coord.y * chunkSize,
            octaves = octaves,
            scale = scale,
            persistence = persistence,
            lacunarity = lacunarity,
            ridgeWeight = ridgeWeight,
            offset = offset,
            outHeights = heights1D,
            regionScale = regionScale,
            basinDepth = basinDepth,
            basinSharpness = basinSharpness
        };

        JobHandle handle = job.Schedule(res * res, 64);
        while (!handle.IsCompleted)
            yield return null;
        handle.Complete();

        float[,] heights = new float[res, res];
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                heights[z, x] = heights1D[z * res + x];
            }
        }
        heights1D.Dispose();

        newData.SetHeights(0, 0, heights);
        newData.SyncHeightmap();

        loadedChunks.Add(coord, chunkTerrain);
    }

    void UnloadChunk(Vector2Int coord)
    {
        if (loadedChunks.TryGetValue(coord, out Terrain terrain))
        {
            Destroy(terrain.gameObject);
            loadedChunks.Remove(coord);
        }
    }

    [BurstCompile]
    struct PerlinJob : IJobParallelFor
    {
        public int res;
        public int worldX;
        public int worldZ;
        public int octaves;
        public float scale;
        public float persistence;
        public float lacunarity;
        public float ridgeWeight;
        public Vector2 offset;

        public float regionScale;
        public float basinDepth;
        public float basinSharpness;

        [WriteOnly] public NativeArray<float> outHeights;

        public void Execute(int index)
        {
            int x = index % res;
            int z = index / res;

            float wx = worldX + x;
            float wz = worldZ + z;

            float regionNoise = Mathf.PerlinNoise(
                (wx + offset.x) * regionScale,
                (wz + offset.y) * regionScale
            );
            regionNoise = Mathf.Pow(regionNoise, basinSharpness);

            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;
            float erosionAccum = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float octaveOffsetX = i * 1.3f;
                float octaveOffsetZ = i * 2.7f;

                float nx = (wx + octaveOffsetX) * frequency * scale + offset.x;
                float nz = (wz + octaveOffsetZ) * frequency * scale + offset.y;

                float baseNoise = Mathf.PerlinNoise(nx, nz);

                float ridge = 1f - Mathf.Abs(baseNoise * 2f - 1f);
                ridge = Mathf.Pow(ridge, ridgeWeight);

                float noise = Mathf.Lerp(baseNoise, ridge, 0.6f);

                float erosionFactor = 1f / (1f + erosionAccum * erosionAccum * 2f);

                total += noise * amplitude * erosionFactor;
                maxValue += amplitude;

                erosionAccum += noise * 0.4f;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            float mountainHeight = total / maxValue;

            float basinHeight = (mountainHeight * 0.2f) - basinDepth;
            float finalHeight = Mathf.Lerp(basinHeight, mountainHeight, regionNoise);

            outHeights[index] = Mathf.Clamp01(finalHeight);
        }
    }

    [CustomEditor(typeof(ChunkedProceduralTerrain))]
    [CanEditMultipleObjects]
    public class ChunkedProceduralTerrainEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ChunkedProceduralTerrain terrainGen = (ChunkedProceduralTerrain)target;
            if (GUILayout.Button("Regenerate Terrain"))
            {
                terrainGen.GenerateTerrain();
            }
        }
    }
}
