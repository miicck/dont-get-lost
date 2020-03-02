using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class chunk
{
    public const int SIZE = 64;
    public const int TERRAIN_RES = SIZE + 1;

    public int x { get; private set; }
    public int z { get; private set; }

    Transform transform;
    Terrain terrain;

    // Coordinate transforms
    int grid_to_world_x(int i) { return x * SIZE + i - SIZE / 2; }
    int grid_to_world_z(int j) { return z * SIZE + j - SIZE / 2; }

    // Set the chunk neighbours, for smooth transitions
    public void update_neighbours(chunk north, chunk east, chunk south, chunk west)
    {
        terrain.SetNeighbors(
            west == null ? null : west.terrain,
            north == null ? null : north.terrain,
            east == null ? null : east.terrain,
            south == null ? null : south.terrain
        );
    }

    public chunk(int x, int z)
    {
        // Save my chunk-coordinates
        this.x = x;
        this.z = z;

        // Create the transform representing this chunk
        transform = new GameObject("chunk_" + x + "_" + z).transform;
        transform.position = new Vector3(x - 0.5f, 0, z - 0.5f) * SIZE;

        // Create the water level
        var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
        water.transform.SetParent(transform);
        water.transform.localPosition = new Vector3(
            SIZE / 2, world.SEA_LEVEL, SIZE / 2);
        water.transform.localScale = Vector3.one * SIZE;
        water.transform.forward = -Vector3.up;
        var ren = water.gameObject.GetComponent<MeshRenderer>();
        ren.material = Resources.Load<Material>("materials/water");

        // Create the terrain object, collider and datastructure
        terrain = new GameObject("terrain").AddComponent<Terrain>();
        var tc = terrain.gameObject.AddComponent<TerrainCollider>();
        terrain.transform.SetParent(transform);
        terrain.transform.localPosition = Vector3.zero;
        var td = new TerrainData();
        terrain.terrainData = td;
        tc.terrainData = td;

        // Create the terrain texture
        SplatPrototype[] splats = new SplatPrototype[1];
        var tex = new Texture2D(TERRAIN_RES, TERRAIN_RES);
        var pixels = new Color[TERRAIN_RES * TERRAIN_RES];
        tex.wrapMode = TextureWrapMode.Clamp;
        splats[0] = new SplatPrototype();
        splats[0].texture = tex;
        splats[0].tileSize = new Vector2(1f, 1f) * SIZE;
        td.splatPrototypes = splats;
        var alphamaps = new float[TERRAIN_RES, TERRAIN_RES, 1];

        // Set the heighmap resolution/scale
        td.heightmapResolution = TERRAIN_RES;
        td.size = new Vector3(SIZE, world.MAX_ALTITUDE, SIZE);
        var heights = new float[TERRAIN_RES, TERRAIN_RES];

        // Map world onto chunk
        for (int i = 0; i < TERRAIN_RES; ++i)
            for (int j = 0; j < TERRAIN_RES; ++j)
            {
                // Get the point descibing this part of the world
                int x_world = grid_to_world_x(i);
                int z_world = grid_to_world_z(j);
                var point = world.point(x_world, z_world);

                // Texture/alphamap
                pixels[j * TERRAIN_RES + i] = point.terrain_color;
                alphamaps[i, j, 0] = 1.0f;

                // Heightmap (note it is the transpose for some reason)
                heights[j, i] = point.altitude / world.MAX_ALTITUDE;
            }

        // Apply the various maps
        tex.SetPixels(pixels);
        tex.Apply();
        td.SetAlphamaps(0, 0, alphamaps);
        td.SetHeights(0, 0, heights);
    }

    public void destroy()
    {
        Object.Destroy(transform.gameObject);
    }
}