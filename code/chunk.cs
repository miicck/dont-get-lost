using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A biome represents a particular kind of geography
public class biome
{
    // Maps a biome to a floating point value
    public delegate float biome_float(biome b);

    public static biome at(int chunk_x, int chunk_z)
    {
        float x = (float)chunk_x * chunk.SIZE;
        float z = (float)chunk_z * chunk.SIZE;

        float p = Mathf.PerlinNoise(x / 256f, z / 256f);
        if (p < 0.33f) return new hills();
        if (p < 0.66f) return new mountains();
        return new ocean();
    }

    // Return the altitude of this biome at (x, z), in meters
    public virtual float altitude(float x, float z) { return 0; }
    public virtual Color terrain_color(float x, float z) { return new Color(0.4f, 0.8f, 0, 0); }

    public class ocean : biome
    {

    }

    public class hills : biome
    {
        public const float HILL_SIZE = 27.2f;

        public override float altitude(float x, float z)
        {
            //return HILL_SIZE;
            return HILL_SIZE *
                Mathf.PerlinNoise(x / HILL_SIZE, z / HILL_SIZE);
        }
    }

    public class mountains : biome
    {
        public const float MOUNTAIN_SIZE = 54.5f;

        public override float altitude(float x, float z)
        {
            //return MOUNTAIN_SIZE;
            return MOUNTAIN_SIZE *
                Mathf.PerlinNoise(x / MOUNTAIN_SIZE, z / MOUNTAIN_SIZE);
        }
    }
}

// Mixes nearby biomes together
public class biome_mixer
{
    public biome[,] local_biomes { get; private set; }
    public int x { get; private set; }
    public int z { get; private set; }

    public biome_mixer(int x, int z)
    {
        // Save the centre-chunk coordinates
        this.x = x;
        this.z = z;

        // Save the local biomes
        local_biomes = new biome[3, 3];
        for (int dx = -1; dx < 2; ++dx)
            for (int dz = -1; dz < 2; ++dz)
                local_biomes[dx + 1, dz + 1] =
                    biome.at(x + dx, z + dz);
    }

    // Get the amount that we should blend in a
    // particular direction, given the fractional
    // coordinate along that direction
    float blend_amount(float fraction, float threshold)
    {
        float f = fraction;
        float t = threshold;
        float ret = 0;
        if (f < t) ret = (t - f) / t;
        else if (f > 1 - t) ret = (f - (1 - t)) / t;
        else ret = 0f;
        return procmath.smooth_max_cos(ret);
    }

    // The coordinate in local_biomes that should be
    // sampled in a particular direction, given the
    // fractional coordinate along that direction
    int blend_coord(float fraction, float threshold)
    {
        float f = fraction;
        float t = threshold;
        if (f < t) return 0;
        if (f > 1 - t) return 2;
        return 1;
    }

    // Combine an array of T, with given weights
    public delegate T combine_func<T>(T[] to_combine, float[] weights);

    // Take a biome and return a T
    public delegate T biome_get<T>(biome b);

    // Mix local biome_get's together according to blend_amount's
    // with a given combine_func
    public T mix<T>(float x, float z,
        biome_get<T> getter, combine_func<T> combiner)
    {
        // Get the fractional coordinates within the chunk
        float xf = (x - this.x * chunk.SIZE) / chunk.SIZE;
        float zf = (z - this.z * chunk.SIZE) / chunk.SIZE;

        // Get the blend amounts and coordinates
        //   xamt = the amount we blend east/west biomes in
        //     (xcoord tells us if we blend east or west)
        float zmod = (zf - 0.5f) * (zf - 0.5f) / 0.25f;
        float xamt = blend_amount(xf, 0.25f + 0.25f * zmod);
        int xcoord = blend_coord(xf, 0.25f + 0.25f * zmod);

        //   zamt = the amount we blend north/south biomes in
        //     (zcoord tells us if we blend north or south)
        float xmod = (xf - 0.5f) * (xf - 0.5f) / 0.25f;
        float zamt = blend_amount(zf, 0.25f + 0.25f * xmod);
        int zcoord = blend_coord(zf, 0.25f + 0.25f * xmod);

        //   damt = the amount we blend the diagonal biome in
        float damt = Mathf.Min(xamt, zamt);

        // Total used to normalise the result
        float tot = 1.0f + xamt + zamt + damt;

        // The things to combine across biomes 
        T[] to_combine = new T[] {
            getter(local_biomes[1,1]),           // Centre
            getter(local_biomes[xcoord,1]),      // East or west
            getter(local_biomes[1,zcoord]),      // North or south
            getter(local_biomes[xcoord, zcoord]) // Diagonal
        };

        // The weights to combine them with
        float[] weights = new float[] {
            1.0f / tot, // Centre amt is always 1.0f
            xamt / tot, // East/west amount
            zamt / tot, // North/south amount
            damt / tot  // Diagonal amount
        };

        // Return the combined result
        return combiner(to_combine, weights);
    }

    // Mix the altitude
    public float altitude(float x, float z)
    {
        return mix(x, z, (b) => b.altitude(x, z), (fs, ws) =>
              {
                  float total = 0;
                  for (int i = 0; i < fs.Length; ++i)
                      total += ws[i] * fs[i];
                  return total;
              });
    }

    // Mix the terrain color
    public Color terrain_color(float x, float z)
    {
        return mix(x, z, (b) => b.terrain_color(x, z), (cs, ws) =>
        {
            Color result = new Color(0, 0, 0, 0);
            for (int i = 0; i < cs.Length; ++i)
            {
                result.a += cs[i].a * ws[i];
                result.r += cs[i].r * ws[i];
                result.g += cs[i].g * ws[i];
                result.b += cs[i].b * ws[i];
            }
            return result;
        });
    }
}

// The in-game representation of a chunk
public class chunk : MonoBehaviour
{
    // The side-length of a map chunk
    public const int SIZE = 64;

    // The terrain resolution is set to SIZE + 1 so that
    // there are exactly SIZE x SIZE terrain grid squares
    public const int TERRAIN_RES = SIZE + 1;

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
        var tex = new Texture2D(TERRAIN_RES, TERRAIN_RES);
        var pixels = new Color[TERRAIN_RES * TERRAIN_RES];
        for (int i = 0; i < TERRAIN_RES; ++i)
            for (int j = 0; j < TERRAIN_RES; ++j)
            {
                float xf = chunk.SIZE * x + i;
                float zf = chunk.SIZE * z + j;
                pixels[i * TERRAIN_RES + j] =
                    chunk.local_biomes.terrain_color(xf, zf);
            }
        tex.SetPixels(pixels);
        tex.Apply();

        splats[0] = new SplatPrototype();
        splats[0].texture = tex;
        splats[0].tileSize = new Vector2(1, 1);
        td.splatPrototypes = splats;

        var alphamaps = new float[TERRAIN_RES, TERRAIN_RES, 1];
        for (int i = 0; i < TERRAIN_RES; ++i)
            for (int j = 0; j < TERRAIN_RES; ++j)
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

        return chunk;
    }
}
