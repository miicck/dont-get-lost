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
    biome.point[,] points = new biome.point[TERRAIN_RES, TERRAIN_RES];

    // Coordinate transforms
    int grid_to_world_x(int i) { return x * SIZE + i - SIZE / 2; }
    int grid_to_world_z(int j) { return z * SIZE + j - SIZE / 2; }

    // Set the chunk neighbours, for smooth transitions
    public void update_neighbours(chunk north, chunk east, chunk south, chunk west)
    {
        if (!generation_complete()) return;
        if (north != null && !north.generation_complete()) return;
        if (east != null && !east.generation_complete()) return;
        if (south != null && !south.generation_complete()) return;
        if (west != null && !west.generation_complete()) return;

        terrain.SetNeighbors(
            west == null ? null : west.terrain,
            north == null ? null : north.terrain,
            east == null ? null : east.terrain,
            south == null ? null : south.terrain
        );
    }

    public chunk(int x, int z)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

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
        terrain.terrainData.heightmapResolution = TERRAIN_RES;
        terrain.terrainData.size = new Vector3(SIZE, world.MAX_ALTITUDE, SIZE);

        // Start the gradual chunk generator
        var generator = new GameObject("generator").AddComponent<gradual_chunk_generator>();
        generator.transform.SetParent(transform);
        generator.chunk = this;

        utils.log("Chunk " + x + ", " + z + " created in " + sw.ElapsedMilliseconds + " ms", "generation");
    }

    // Continue generation, bit by bit
    // returns true when generation is complete
    bool continue_generation()
    {
        if (continue_set_points())
            if (continue_set_alphamaps())
                if (continue_apply_heights())
                    if (continue_gen_objects())
                        return true;
        return false;
    }

    bool generation_complete()
    {
        return points_i >= TERRAIN_RES &&
               alphamaps_i >= TERRAIN_RES &&
               heights_i >= TERRAIN_RES &&
               object_generation_attempts_remaining <= 0;
    }

    int points_i = 0;
    bool continue_set_points()
    {
        if (points_i >= TERRAIN_RES)
            return true;

        int i = points_i;
        for (int j = 0; j < TERRAIN_RES; ++j)
        {
            // Get the point descibing this part of the world
            int x_world = grid_to_world_x(i);
            int z_world = grid_to_world_z(j);
            var point = world.point(x_world, z_world);
            points[i, j] = point;
            point.apply_global_rules();
        }

        ++points_i;
        return false;
    }

    int alphamaps_i = 0;
    float[,,] alphamaps = new float[TERRAIN_RES, TERRAIN_RES, 1];
    Color[] pixels = new Color[TERRAIN_RES * TERRAIN_RES];
    bool continue_set_alphamaps()
    {
        if (alphamaps_i >= TERRAIN_RES)
            return true;

        // Generate alphamaps
        int i = alphamaps_i;
        for (int j = 0; j < TERRAIN_RES; ++j)
        {
            pixels[j * TERRAIN_RES + i] = points[i, j].terrain_color;
            alphamaps[i, j, 0] = 1.0f;
        }

        if (alphamaps_i == TERRAIN_RES - 1)
        {
            // Create the terrain texture
            SplatPrototype[] splats = new SplatPrototype[1];
            var tex = new Texture2D(TERRAIN_RES, TERRAIN_RES);

#if         UNITY_2018_3_OR_NEWER // New terrain system
            tex.wrapMode = TextureWrapMode.Clamp;
            var terrain_layers = new TerrainLayer[1];
            terrain_layers[0] = new TerrainLayer();
            terrain_layers[0].diffuseTexture = tex;
            terrain_layers[0].tileSize = new Vector2(1f, 1f) * SIZE;
            terrain.terrainData.terrainLayers = terrain_layers;
            terrain.materialTemplate = Resources.Load<Material>("materials/terrain");

#           else // Old terrain system
            tex.wrapMode = TextureWrapMode.Clamp;
            splats[0] = new SplatPrototype();
            splats[0].texture = tex;
            splats[0].tileSize = new Vector2(1f, 1f) * SIZE;
            terrain.terrainData.splatPrototypes = splats;
#           endif

            // Apply the alphmaps
            tex.SetPixels(pixels);
            tex.Apply();
            terrain.terrainData.SetAlphamaps(0, 0, alphamaps);

            alphamaps = null;
            pixels = null;
        }

        ++alphamaps_i;
        return false;
    }

    int heights_i = 0;
    float[,] heights = new float[TERRAIN_RES, TERRAIN_RES];
    bool continue_apply_heights()
    {
        if (heights_i >= TERRAIN_RES) return true;

        // Map world onto chunk
        int i = heights_i;
        for (int j = 0; j < TERRAIN_RES; ++j)
            // Heightmap (note it is the transpose for some reason)
            heights[j, i] = points[i, j].altitude / world.MAX_ALTITUDE;

        if (heights_i == TERRAIN_RES - 1)
        {
            // Apply the heigtmap
            terrain.terrainData.SetHeights(0, 0, heights);
            heights = null;
        }

        ++heights_i;
        return false;
    }

    int object_generation_attempts_remaining = TERRAIN_RES * TERRAIN_RES / 100;
    bool continue_gen_objects()
    {
        // Generate objects
        if (object_generation_attempts_remaining <= 0)
            return true;
        --object_generation_attempts_remaining;

        int i = Random.Range(0, TERRAIN_RES);
        int j = Random.Range(0, TERRAIN_RES);

        int x_world = grid_to_world_x(i);
        int z_world = grid_to_world_z(j);
        var point = points[i, j];

        // Get a suggested object from the map 
        var suggested_object = world.suggest_object(x_world, z_world);
        if (suggested_object == null) return false;

        // Check the world object can be placed here
        var wo = world_object.look_up(suggested_object);

        float xf = i / (float)TERRAIN_RES;
        float zf = j / (float)TERRAIN_RES;
        Vector3 terrain_normal = terrain.terrainData.GetInterpolatedNormal(xf, zf);
        if (!wo.can_place(point, terrain_normal)) return false;

        // Place the world object
        wo = wo.inst();
        wo.transform.SetParent(transform);
        wo.transform.position = new Vector3(x_world, point.altitude, z_world);
        wo.on_placement(terrain_normal);

        return false;
    }

    class gradual_chunk_generator : MonoBehaviour
    {
        public chunk chunk;

        private void Update()
        {
            // Generate every frame until
            // we need not generate any more
            if (chunk.continue_generation())
            {
                Destroy(this.gameObject);
                return;
            }
        }
    }

    public void destroy()
    {
        // Finish generation
        while (!continue_generation()) { }

        // Destroy the object
        Object.Destroy(transform.gameObject);
    }
}