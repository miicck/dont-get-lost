using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The in-game representation of a chunk
public class chunk : MonoBehaviour
{
    Terrain terrain;
    world_generator.chunk_info chunk_info;

    // The side-length of a map chunk
    public const int SIZE = 256;
    public const int TERRAIN_RES = SIZE + 1;

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

        // Load the chunk from disk
        chunk.chunk_info = new world_generator.chunk_info("world_1", x, z);

        // Create the terrain heighmap
        var td = new TerrainData();
        chunk.terrain.terrainData = td;
        tc.terrainData = td;

        // Set the heighmap resolution to the
        // chunk size + 1. This is done so there are
        // exactly size x size terrain squares.
        int hres = SIZE + 1;
        td.heightmapResolution = hres;
        td.size = new Vector3(SIZE, world.MAX_ALTITUDE, SIZE);

        // Fill the heightmap (note that heights[i,j] = 1.0 corresponds
        // to an actual height of world.MAX_ALTITUDE).
        var heights = new float[hres, hres];
        for (int xt = 0; xt < hres; ++xt)
            for (int zt = 0; zt < hres; ++zt)
                heights[zt, xt] = chunk.chunk_info.altitude[xt, zt];

        // Assign the heightmap to the terrain data
        td.SetHeights(0, 0, heights);

        return chunk;
    }
}
