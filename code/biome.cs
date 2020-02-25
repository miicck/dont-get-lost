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
    public virtual Color terrain_color(float x, float z) { return new Color(0.4f, 0.6f, 0.2f, 0); }

    public class ocean : biome
    {
        public override Color terrain_color(float x, float z)
        {
            return new Color(0.8f, 0.8f, 0f, 0f);
        }
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

		public override Color terrain_color(float x, float z)
        {
            return new Color(0.5f, 0.5f, 0.5f, 0f);
        }

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