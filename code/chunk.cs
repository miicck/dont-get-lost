using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class chunk : MonoBehaviour
{
    Terrain terrain;

    // The side-length of a map chunk
    public const float SIZE = 256f;

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

        // Create the terrain heighmap
        var td = new TerrainData();
        chunk.terrain.terrainData = td;
        tc.terrainData = td;

        // Set the heighmap resolution to the
        // chunk size + 1. This is done so there are
        // exactly size x size terrain squares.
        int hres = (int)SIZE + 1;
        td.heightmapResolution = hres;
        td.size = new Vector3(SIZE, world.MAX_ALTITUDE, SIZE);

        // Fill the heightmap (note that heights[i,j] = 1.0 corresponds
        // to an actual height of world.MAX_ALTITUDE).
        var heights = new float[hres, hres];
        for (int xt = 0; xt < hres; ++xt)
        {
            float xf = x * chunk.SIZE + (float)xt;
            for (int zt = 0; zt < hres; ++zt)
            {
                float zf = z * chunk.SIZE + (float)zt;
                heights[zt, xt] = world.altitude(xf, zf) / world.MAX_ALTITUDE;
            }
        }

        // Assign the heightmap to the terrain data
        td.SetHeights(0, 0, heights);

        return chunk;
    }
}
