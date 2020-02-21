using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A biome represents a particular kind of geography
public class biome
{
    // Maps a biome to a floating point value
    public delegate float biome_float(biome b);

    // Blend biome_float values together to get an overall 
    // blended-biome value at x, z
    public static float blend_biomes(float x, float z, biome_float del)
    {
        const float PERIOD = 512f;

        float p = Mathf.PerlinNoise(x / PERIOD, z / PERIOD);
        if (p > 0.6f) p = 1f;
        else if (p < 0.4f) p = 0f;
        else p = (p - 0.4f) / 0.2f;

        var ocean_val = del(new ocean());
        var hill_val = del(new hills());

        return p * hill_val + (1 - p) * ocean_val;
    }

    // Return the altitude of this biome at (x, z), in meters
    public virtual float altitude(float x, float z) { return 0; }

    public class ocean : biome
    {

    }

    public class hills : biome
    {
        public const float HILL_SIZE = 64f;

        public override float altitude(float x, float z)
        {
            return HILL_SIZE *
                Mathf.PerlinNoise(x / HILL_SIZE, z / HILL_SIZE);
        }
    }
}

// The in-game representation of a chunk
public class chunk : MonoBehaviour
{
    // The side-length of a map chunk
    public const int SIZE = 256;

    // The terrain resolution is set to SIZE + 1 so that
    // there are exactly SIZE x SIZE terrain grid squares
    public const int TERRAIN_RES = SIZE + 1;

    // The terrain of this chunk
    Terrain terrain;

    public int x { get; private set; }
    public int z { get; private set; }

    // Check if the chunk coords are the same as those given
    public bool check_coords(int x, int z)
    {
        return this.x == x && this.z == z;
    }

    // Check if this chunk is out of chunk range
    // from the given player coordinates
    public bool in_range(int player_x, int player_z)
    {
        if (this.x < player_x - world.CHUNK_RANGE) return false;
        if (this.x > player_x + world.CHUNK_RANGE) return false;
        if (this.z < player_z - world.CHUNK_RANGE) return false;
        if (this.z > player_z + world.CHUNK_RANGE) return false;
        return true;
    }

    // Set the neighbouring chunks of this chunk
    public void update_neighbours(chunk north, chunk east, chunk south, chunk west)
    {
        Terrain left = east == null ? null : east.terrain;
        Terrain right = west == null ? null : west.terrain;
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
        chunk.transform.position = new Vector3(x, 0, z) * SIZE;

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
                heights[zt, xt] = biome.blend_biomes(xf, zf,
                    (b) => b.altitude(xf, zf)) / world.MAX_ALTITUDE;
            }

        // Assign the heightmap to the terrain data
        td.SetHeights(0, 0, heights);

        return chunk;
    }
}
