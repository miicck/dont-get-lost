using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class chunk
{
    public const int SIZE = 64;
    public const int TERRAIN_RES = SIZE + 1;

    public int x { get; private set; }
    public int z { get; private set; }

    public Transform transform { get; private set; }
    Terrain terrain;
    biome.point[,] points = new biome.point[TERRAIN_RES, TERRAIN_RES];
    item[] items { get { return transform.GetComponentsInChildren<item>(); } }

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

    // The transform containing all of the chunks
    static Transform _chunk_container;
    static Transform chunk_container
    {
        get
        {
            if (_chunk_container == null)
                _chunk_container = new GameObject("chunks").transform;
            return _chunk_container;
        }
    }

    public chunk(int x, int z)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Save my chunk-coordinates
        this.x = x;
        this.z = z;

        // Create the transform representing this chunk
        transform = new GameObject("chunk_" + x + "_" + z).transform;
        transform.SetParent(chunk_container);
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
    delegate bool gen_func();
    gen_func[] gen_todo;
    int gen_stage = 0;
    System.Diagnostics.Stopwatch gen_sw;

    bool continue_generation()
    {
        if (gen_todo == null)
        {
            // Create the generation stages
            gen_todo = new gen_func[]
            {
                continue_set_points,
                continue_set_alphamaps,
                continue_apply_heights,
                continue_gen_objects
            };
            gen_sw = System.Diagnostics.Stopwatch.StartNew();
        }

        // Generation complete
        if (gen_stage >= gen_todo.Length)
            return true;

        // Continue this generation stage
        if (gen_todo[gen_stage]())
        {
            utils.log("Chunk " + x + "_" + z + " generation stage: " + gen_stage +
                      " took " + gen_sw.ElapsedMilliseconds + " ms", "chunk_generation");
            // Advance generation stage
            gen_stage++;
        }

        return false;
    }

    bool generation_complete()
    {
        return points_i >= TERRAIN_RES &&
               alphamaps_i >= TERRAIN_RES &&
               heights_i >= TERRAIN_RES &&
               objects_i >= SIZE;
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
            points[i, j] = world.point(x_world, z_world);
            points[i, j].apply_global_rules();
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
        for (int j = 0; j < TERRAIN_RES; ++j)
            // Heightmap (note it is the transpose for some reason)
            heights[j, heights_i] = points[heights_i, j].altitude / world.MAX_ALTITUDE;
        ++heights_i;

        if (heights_i >= TERRAIN_RES)
        {
            // Apply the heigtmap
            terrain.terrainData.SetHeights(0, 0, heights);
            heights = null;
        }
        return false;
    }

    int objects_i = 0;
    bool continue_gen_objects()
    {
        if (objects_i >= SIZE) return true;

        // Generate objects
        int i = objects_i;
        for (int j = 0; j < SIZE; ++j)
        {
            var point = points[i, j];

            // Check if there is a world object at this point
            if (point.world_object_gen == null) continue;

            // Get some coodinate information
            int x_world = grid_to_world_x(i);
            int z_world = grid_to_world_z(j);
            float xf = i / (float)TERRAIN_RES;
            float zf = j / (float)TERRAIN_RES;
            Vector3 terrain_normal = terrain.terrainData.GetInterpolatedNormal(xf, zf);

            // Need to generate from prefab
            if (point.world_object_gen.to_generate != null)
            {
                // Check if we can generate the object here
                if (!point.world_object_gen.to_generate.can_place(point, terrain_normal))
                {
                    // We can't, unschedule generation
                    point.world_object_gen = null;
                    continue;
                }
            }

            // Generate (or load) the world object
            var wo = point.world_object_gen.gen_or_load(terrain_normal, point);

            // Place the world object
            wo.transform.SetParent(transform);
            wo.transform.position = new Vector3(x_world, point.altitude, z_world);
        }

        ++objects_i;
        return false;
    }

    class gradual_chunk_generator : MonoBehaviour
    {
        public chunk chunk;
        static int steps_per_frame = 1;

        private void Update()
        {
            // Do more generation steps if the framerate is
            // acceptably high
            int framerate = (int)(1 / Time.deltaTime);
            if (framerate < 60) --steps_per_frame;
            else ++steps_per_frame;

            // Generation must always progress
            if (steps_per_frame < 1) steps_per_frame = 1;

            // Generate every frame until
            // we need not generate any more
            for (int step = 0; step < steps_per_frame; ++step)
                if (chunk.continue_generation())
                {
                    // Generation complete, destroy
                    // this generator
                    Destroy(gameObject);
                    return;
                }
        }
    }

    // Sleeping objects are those who's chunks have been
    // destroyed but might be reloaded in the near future.
    // Only if a biome is destroyed will the objects in it
    // be saved to disk/destroyed.
    static Transform _all_sleeping_objects;
    Transform _sleeping_objects;
    Transform sleeping_objects
    {
        get
        {
            // Top level transform contains the sleeping
            // objects for all chunks
            if (_all_sleeping_objects == null)
                _all_sleeping_objects =
                    new GameObject("sleeping_objects").transform;

            // Create (or find) the transform containing sleeping
            // objects from this chunk
            if (_sleeping_objects == null)
            {
                string so_name = "chunk_" + x + "_" + z;
                _sleeping_objects = _all_sleeping_objects.Find(so_name);
                if (_sleeping_objects == null)
                {
                    _sleeping_objects = new GameObject(so_name).transform;
                    _sleeping_objects.transform.SetParent(_all_sleeping_objects);
                }
            }

            return _sleeping_objects;
        }
    }

    public void destroy()
    {
        // Finish generation
        while (!continue_generation()) { }

        // Detatch and disable world objects
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                if (points[i, j].world_object_gen == null)
                    continue;

                var wo = points[i, j].world_object_gen.generated;
                wo.transform.SetParent(sleeping_objects);
                wo.gameObject.SetActive(false);
            }

        // Destroy the object
        Object.Destroy(transform.gameObject);
    }
}