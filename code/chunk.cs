using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A chunk of the world
public class chunk : MonoBehaviour
{
    // The side-length of a map chunk
    public const int SIZE = 64;

    // The terrain resolution is set to SIZE + 1 so that
    // there are exactly SIZE x SIZE terrain grid squares
    public const int TERRAIN_RES = SIZE + 1;

    // The terrain texutre resolution should be
    // a power of two
    public const int TERRAIN_TEX_RES = 64;

    // The biomes immediately surrounding this chunk
    biome_mixer local_biomes;

    // The terrain of this chunk
    Terrain terrain;

    public int x { get; private set; }
    public int z { get; private set; }

    // Check if the chunk coords are the same as those given
    public bool check_coords(int x, int z)
    {
        return this.x == x && this.z == z;
    }

    // Set the neighbouring chunks of this chunk
    public void update_neighbours(chunk north, chunk east, chunk south, chunk west)
    {
        Terrain right = east == null ? null : east.terrain;
        Terrain left = west == null ? null : west.terrain;
        Terrain top = north == null ? null : north.terrain;
        Terrain bottom = south == null ? null : south.terrain;
        terrain.SetNeighbors(left, top, right, bottom);
    }

    // Generate and return the x^th, z^th map chunk
    public static chunk generate(int x, int z)
    {
        // Create the object hierarchy
        var chunk = new GameObject("chunk_" + x + "_" + z).AddComponent<chunk>();
        chunk.x = x;
        chunk.z = z;
        chunk.terrain = new GameObject("terrain").AddComponent<Terrain>();
        var tc = chunk.terrain.gameObject.AddComponent<TerrainCollider>();
        chunk.terrain.transform.SetParent(chunk.transform);
        chunk.transform.position =
            new Vector3((float)x - 0.5f, 0, (float)z - 0.5f) * SIZE;

        // Get biome information around this chunk
        chunk.local_biomes = new biome_mixer(x, z);

        // Create the water level
        var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
        water.transform.SetParent(chunk.transform);
        water.transform.localPosition = new Vector3(
            chunk.SIZE / 2, world.SEA_LEVEL, chunk.SIZE / 2);
        water.transform.localScale = Vector3.one * chunk.SIZE;
        water.transform.forward = -Vector3.up;
        var ren = water.gameObject.GetComponent<MeshRenderer>();
        ren.material = Resources.Load<Material>("materials/water");

        // Create the terrain heighmap
        var td = new TerrainData();
        chunk.terrain.terrainData = td;
        tc.terrainData = td;

        // Create the terrain texture
        SplatPrototype[] splats = new SplatPrototype[1];
        var tex = new Texture2D(TERRAIN_TEX_RES, TERRAIN_TEX_RES);
        var pixels = new Color[TERRAIN_TEX_RES * TERRAIN_TEX_RES];
        for (int i = 0; i < TERRAIN_TEX_RES; ++i)
            for (int j = 0; j < TERRAIN_TEX_RES; ++j)
            {
                float xf = chunk.SIZE * x + i;
                float zf = chunk.SIZE * z + j;
                pixels[j * TERRAIN_TEX_RES + i] =
                    chunk.local_biomes.terrain_color(xf, zf);
            }
        tex.SetPixels(pixels);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();

        splats[0] = new SplatPrototype();
        splats[0].texture = tex;
        splats[0].tileSize = new Vector2(1f, 1f) * chunk.SIZE;
        td.splatPrototypes = splats;

        var alphamaps = new float[TERRAIN_TEX_RES, TERRAIN_TEX_RES, 1];
        for (int i = 0; i < TERRAIN_TEX_RES; ++i)
            for (int j = 0; j < TERRAIN_TEX_RES; ++j)
                alphamaps[i, j, 0] = 1.0f;
        td.SetAlphamaps(0, 0, alphamaps);

        // Set the heighmap resolution/scale
        td.heightmapResolution = TERRAIN_RES;
        td.size = new Vector3(SIZE, world.MAX_ALTITUDE, SIZE);

        // Fill the heightmap (note that heights[i,j] = 1.0 corresponds
        // to an actual height of world.MAX_ALTITUDE).
        var heights = new float[TERRAIN_RES, TERRAIN_RES];
        for (int xt = 0; xt < TERRAIN_RES; ++xt)
            for (int zt = 0; zt < TERRAIN_RES; ++zt)
            {
                // Get the global x and z coordinates
                float xf = chunk.SIZE * x + xt;
                float zf = chunk.SIZE * z + zt;
                heights[zt, xt] = chunk.local_biomes.altitude(xf, zf) /
                    world.MAX_ALTITUDE;
            }

        // Assign the heightmap to the terrain data
        td.SetHeights(0, 0, heights);

        // Add world_objects to the chunk
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                if (Random.Range(0, 100) != 0)
                    continue;

                float alt = heights[j, i] * world.MAX_ALTITUDE;
                if (alt < world.SEA_LEVEL + 1.0f) continue;

                var t = world_object.create_random("trees");
                t.transform.SetParent(chunk.transform);
                t.transform.localPosition = new Vector3(i, alt, j);

                t.transform.localScale = Vector3.one * Random.Range(0.6f, 1.4f);
                t.transform.Rotate(0, Random.Range(0, 360f), 0);
            }

        return chunk;
    }
}
