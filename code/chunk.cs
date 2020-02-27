using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A chunk of the world
public class chunk : MonoBehaviour
{
    // Information about a particular
    // location within a chunk
    public class location
    {
        public chunk chunk;
        public float x_world;
        public float z_world;
        public float altitude;
        public Vector3 world_position { get { return new Vector3(x_world, altitude, z_world); } }
        public Vector3 terrain_normal;
        public Color terrain_color;
        public float fertility;

        // Return the terrain angle (steepness) in degrees
        public float terrain_angle
        {
            get
            {
                return Vector3.Angle(Vector3.up, terrain_normal);
            }
        }

        public string debug_info()
        {
            // Get debug information, in string form, pertaining to this location
            string ret = "";
            ret += "X: " + x_world + " (" + (x_world - chunk.transform.position.x) + " in chunk) ";
            ret += "Z: " + z_world + " (" + (z_world - chunk.transform.position.z) + " in chunk) \n";
            ret += "Terrain altitude : " + altitude + "\n";
            ret += "Biome mix: " + chunk.local_biomes.get_info(x_world, z_world) + "\n";
            ret += "Terrain normal: " + terrain_normal + " (angle: " + terrain_angle + ")\n";
            ret += "Terrain color: " + terrain_color + "\n";
            ret += "Fertility: " + fertility + "\n";
            return ret;
        }
    }

    // Get the location infromation at the given position
    public location location_at(Vector3 position)
    {
        // Get the position within this chunk
        position -= transform.position;
        int i = (int)position.x;
        int j = (int)position.z;
        if (!utils.in_range(i, SIZE)) return null;
        if (!utils.in_range(j, SIZE)) return null;
        return locations[i, j];
    }

    // The side-length of a map chunk
    public const int SIZE = 64;

    // The location information
    location[,] locations;

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
        // Create the basic chunk object
        var chunk = new GameObject("chunk_" + x + "_" + z).AddComponent<chunk>();
        chunk.transform.position = new Vector3(x, 0, z) * SIZE;
        chunk.x = x;
        chunk.z = z;

        // Actually generate the chunk
        chunk.generate();
        return chunk;
    }

    // Generate this chunk
    void generate()
    {
        // Get biome information around this chunk
        local_biomes = new biome_mixer(x, z);

        // Fill the location information
        locations = new location[TERRAIN_RES, TERRAIN_RES];
        for (int i = 0; i < TERRAIN_RES; ++i)
            for (int j = 0; j < TERRAIN_RES; ++j)
            {
                // Sample the location info from 
                // the local_biomes biome_mixer
                var loc = locations[i, j] = new location();
                loc.chunk = this;
                loc.x_world = (int)(x * SIZE + i);
                loc.z_world = (int)(z * SIZE + j);
                loc.altitude = local_biomes.altitude(loc.x_world, loc.z_world);
                loc.terrain_color = local_biomes.terrain_color(loc.x_world, loc.z_world);
                loc.fertility = local_biomes.fertility(loc.x_world, loc.z_world);
            }

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
        var tex = new Texture2D(TERRAIN_TEX_RES, TERRAIN_TEX_RES);
        var pixels = new Color[TERRAIN_TEX_RES * TERRAIN_TEX_RES];
        for (int i = 0; i < TERRAIN_TEX_RES; ++i)
            for (int j = 0; j < TERRAIN_TEX_RES; ++j)
                pixels[j * TERRAIN_TEX_RES + i] = locations[i, j].terrain_color;
        tex.SetPixels(pixels);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();

        splats[0] = new SplatPrototype();
        splats[0].texture = tex;
        splats[0].tileSize = new Vector2(1f, 1f) * SIZE;
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
        for (int i = 0; i < TERRAIN_RES; ++i)
            for (int j = 0; j < TERRAIN_RES; ++j)
                heights[i, j] = locations[j, i].altitude / world.MAX_ALTITUDE;

        // Assign the heightmap to the terrain data
        td.SetHeights(0, 0, heights);

        // Sample terrain normals from the terrain data object
        for (int i = 0; i < TERRAIN_RES; ++i)
            for (int j = 0; j < TERRAIN_RES; ++j)
                locations[i, j].terrain_normal = td.GetInterpolatedNormal(
                    i / (float)TERRAIN_RES, j / (float)TERRAIN_RES);

        // Add world_objects to the chunk
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                if (Random.Range(0, 50) != 0) continue;
                local_biomes.spawn_world_object(locations[i, j]);
            }
    }
}
