using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// A biome represents a particular kind of geography
// biomes are rasterised on the same grid size as chunks
// and interpolated between chunks via the biome_mixer class.
public class biome
{
    // The approximate size of a biome
    const float SIZE = 512f;

    // Constructors for all biome types and a corresponding
    // random number in [0,1] for each
    static List<ConstructorInfo> biome_constructors = null;
    static List<float> biome_random_numbers = null;

    // Return the biome at the given chunk coordinates
    public static biome at(int chunk_x, int chunk_z)
    {
        if (biome_constructors == null)
        {
            // Find the biome types
            biome_constructors = new List<ConstructorInfo>();
            biome_random_numbers = new List<float>();
            var asem = Assembly.GetAssembly(typeof(biome));
            var types = asem.GetTypes();
            foreach (var t in types)
            {
                // Check if this type is a valid biome
                if (!t.IsSubclassOf(typeof(biome))) continue;
                if (t.IsAbstract) continue;

                // Compile the list of biome constructors
                var c = t.GetConstructor(new System.Type[0]);
                biome_constructors.Add(c);
                biome_random_numbers.Add(Random.Range(0, 1f));
            }
        }

        // Map chunk coordinates onto real-world coordinates
        // so perlin noise is generated in real world units
        float x = (float)(chunk_x - 0.5f) * chunk.SIZE;
        float z = (float)(chunk_z - 0.5f) * chunk.SIZE;

        // Sample perlin noise amounts for each biome and generate
        // the biome with the maximum value
        float max_perl = float.MinValue;
        int max_index = 0;
        for (int i = 0; i < biome_constructors.Count; ++i)
        {
            float r = biome_random_numbers[i];
            float p = Mathf.PerlinNoise(x / SIZE + r, z / SIZE - r);
            if (p > max_perl)
            {
                max_perl = p;
                max_index = i;
            }
        }
        return (biome)biome_constructors[max_index].Invoke(null);
    }

    // Return the altitude of this biome at (x, z), in meters
    public virtual float altitude(float x, float z) { return 0; }

    // Return how fertile the ground is in [0, 1] at x, z
    public virtual float fertility(float x, float z) { return 1.0f; }

    // The color of the ground at a given x, z coordinate
    public virtual Color terrain_color(float x, float z) { return new Color(0.4f, 0.6f, 0.2f, 0); }

    // The world objects that can be generated in this biome
    public virtual string[] world_objects() { return new string[0]; }

    // Get a world object from the world_objects list, given
    // the location specification
    public string get_world_object(chunk.location location)
    {
        var objs = world_objects();
        if (objs.Length == 0) return null;
        return objs[Random.Range(0, objs.Length)];
    }

    public class ocean : biome
    {
        public override Color terrain_color(float x, float z)
        {
            // Yellow sand
            return new Color(0.8f, 0.8f, 0f, 0f);
        }
    }

    public class grass_islands : biome
    {
        public const float HILL_SIZE = 27.2f;

        public override string[] world_objects()
        {
            return new string[] { "tree1" };
        }

        public override float altitude(float x, float z)
        {
            return HILL_SIZE *
                Mathf.PerlinNoise(x / HILL_SIZE, z / HILL_SIZE);
        }
    }

    public class mountains : biome
    {
        public const float MOUNTAIN_SIZE = 134.5f;
        public const float HILL_SIZE = 34.4f;
        public const float CLIFF_PERIOD = 54.2f;
        public const float MAX_CLIFF_HEIGHT = 5f;

        public override string[] world_objects()
        {
            return new string[] { "rocks1" };
        }

        public override Color terrain_color(float x, float z)
        {
            // White cliff, grey rock
            float c = clifness(x, z);
            float b = 0.5f + 0.5f * c;
            return new Color(b, b, b, 0f);
        }

        float clifness(float x, float z)
        {
            float val = Mathf.PerlinNoise(x / CLIFF_PERIOD, z / CLIFF_PERIOD);
            if (val < 0.5f) return 0;
            else return (val - 0.5f) * 2f;
        }

        public override float fertility(float x, float z)
        {
            // Few plants grow on mountain
            return 0.25f;
        }

        public override float altitude(float x, float z)
        {
            float cliff = clifness(x, z);

            float m1 = MOUNTAIN_SIZE *
                Mathf.PerlinNoise(x / MOUNTAIN_SIZE, z / MOUNTAIN_SIZE);

            float m2 = HILL_SIZE *
                Mathf.PerlinNoise(x / HILL_SIZE, z / HILL_SIZE);

            float alt = m1 + m2;
            float cliff_alt = MAX_CLIFF_HEIGHT * Mathf.Floor(alt / MAX_CLIFF_HEIGHT);

            return alt * (1 - cliff) + cliff_alt * cliff;
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

    // A simple combine_func to take a simple weighted average
    public static float combine_func_average(float[] fs, float[] ws)
    {
        float total = 0;
        for (int i = 0; i < fs.Length; ++i)
            total += ws[i] * fs[i];
        return total;
    }

    // Take a biome and return a T
    public delegate T biome_get<T>(biome b);

    // Mix local biome_get's together according to blend_amount's
    // with a given combine_func
    public T mix<T>(float x, float z,
        biome_get<T> getter, combine_func<T> combiner)
    {
        const float MIN_THR = 0.25f;
        const float DEL_THR = 0.25f;

        // Get the fractional coordinates within the chunk
        float xf = (x - this.x * chunk.SIZE) / chunk.SIZE;
        float zf = (z - this.z * chunk.SIZE) / chunk.SIZE;

        // Get the blend amounts and coordinates
        //   xamt = the amount we blend east/west biomes in
        //     (xcoord tells us if we blend east or west)
        float zmod = (zf - 0.5f) * (zf - 0.5f) / 0.25f;
        float xamt = blend_amount(xf, MIN_THR + DEL_THR * zmod);
        int xcoord = blend_coord(xf, MIN_THR + DEL_THR * zmod);

        //   zamt = the amount we blend north/south biomes in
        //     (zcoord tells us if we blend north or south)
        float xmod = (xf - 0.5f) * (xf - 0.5f) / 0.25f;
        float zamt = blend_amount(zf, MIN_THR + DEL_THR * xmod);
        int zcoord = blend_coord(zf, MIN_THR + DEL_THR * xmod);

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
        return mix(x, z, (b) => b.altitude(x, z), combine_func_average);
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

    // Mix the fertility
    public float fertility(float x, float z)
    {
        return mix(x, z, (b) => b.fertility(x, z), combine_func_average);
    }

    // Get information on the biome mix
    public string get_info(float x, float z)
    {
        return mix(x, z, (b) => b.GetType().Name, (ss, ws) =>
        {
            SortedDictionary<string, float> vals = 
                new SortedDictionary<string, float>();
            for (int i = 0; i < ss.Length; ++i)
            {
                if (!vals.ContainsKey(ss[i])) vals[ss[i]] = 0;
                vals[ss[i]] += ws[i];
            }

            string ret = "";
            foreach (var kv in vals)
                ret += kv.Key + ": " + kv.Value + " ";
            return ret;
        });
    }

    // Get the name of a world object that should be generated at
    // the given location
    string get_world_object_name(chunk.location location)
    {
        return mix(x, z, (b) => b.get_world_object(location), (wos, ws) =>
        {
            if (ws.Length == 0)
                return null;

            // Work out the total weight
            float total_weight = 0;
            for (int i = 0; i < ws.Length; ++i)
                total_weight += ws[i];

            // Choose a random world_object by generating a random
            // number in [0, total_weight] and mapping it to a list entry
            float rand = Random.Range(0, total_weight);
            total_weight = 0;
            for (int i = 0; i < ws.Length; ++i)
            {
                total_weight += ws[i];
                if (total_weight > rand)
                    return wos[i];
            }

            return null;
        });
    }

    // Use the above method to generate a world object given a location
    public world_object spawn_world_object(chunk.location location)
    {
        string name = get_world_object_name(location);
        if (name == null) return null;
        return world_object.spawn(name, location);
    }
}