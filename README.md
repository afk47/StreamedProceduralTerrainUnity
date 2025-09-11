# Chunked Procedural Terrain (Unity)

A Unity component for generating infinite procedural terrain using chunk streaming and multithreaded Perlin noise (via Burst + Jobs).  
Terrain loads around the player in chunks and unloads when outside render distance. Includes editor tooling for regeneration. 
Not intended to be used as-is, Modify this code to fit your game/project's needs


https://github.com/user-attachments/assets/ff2bd78e-ab6b-457c-b858-16b3c30d0eed

---

## Features
- **Chunk-based streaming**: Terrain is divided into square chunks, loaded and unloaded based on player position.
- **Procedural heightmaps**: Generated with Perlin noise, octaves, lacunarity, persistence, and ridge blending.
- **Basins and ridges**: Region-scale noise controls large terrain features (valleys, mountains).
- **Unity Jobs + Burst**: Heightmap generation is multithreaded and efficient.
- **Editor button**: Regenerate terrain from the inspector.

---

## Requirements
- Unity 6 (tested)
---

## Usage
1. Create a **Terrain prefab** with desired `TerrainData` and assign to `terrainPrefab`.
2. Add the **ChunkedProceduralTerrain** script to an empty GameObject.
3. Assign:
   - **Player**: Transform to track (usually the player).
   - **Terrain Prefab**: Your base terrain.
   - **Chunk Settings**: Chunk size and render distance.
   - **Noise Settings**: Depth, scale, octaves, lacunarity, persistence, ridge weight, offset.
   - **Region Settings**: Basin depth, sharpness, and region scale.
4. Enter Play mode. Chunks generate around the player automatically.
5. Use the **"Regenerate Terrain"** button in the inspector to reload.

---

## Parameters

### Player & Chunk Settings
- **Player**: Transform followed.
- **Terrain Prefab**: Base terrain prefab, when generating a new terrain chunk it will use the textures set in this prefab 
- **Chunk Size**: Resolution of each terrain tile.
- **Render Distance**: Number of chunks in each direction.

### Terrain Noise Settings
- **Depth**: Terrain height scale.
- **Scale**: Noise frequency.
- **Octaves**: Layers of Perlin noise.
- **Lacunarity**: Frequency multiplier per octave.
- **Persistence**: Amplitude decay per octave.
- **Ridge Weight**: Controls ridge sharpening.
- **Offset**: Global XY noise offset.

### Region Settings
- **Region Scale**: Controls large-scale terrain variation.
- **Basin Depth**: Depth of valleys.
- **Basin Sharpness**: Controls falloff between valleys and mountains.

---

## Code Structure
- **ChunkedProceduralTerrain**: Main MonoBehaviour. Handles chunk management.
- **PerlinJob**: IJobParallelFor struct. Generates heightmaps with noise.
- **ChunkedProceduralTerrainEditor**: Custom inspector with regeneration button.

---

## Notes
- `UpdateChunks()` coroutine runs continuously, loading and unloading chunks.
- Uses `NativeArray<float>` for height storage and `JobHandle` for async execution.
- Terrain collider updates automatically.
- Unused chunks are destroyed to save memory.
- Fractal Brownian Motion Algorithm used is somewhat modified for my use case, you can reimplement as
  basic stacked perlin noise without ridges or basins
- if you have weird symmetry, try using an offset, perlin noise in unity is symmetrical at the origin
---

## License
MIT
