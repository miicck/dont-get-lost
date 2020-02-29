using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// A biome represents the largest singly-generated area of the map
// and is used to define quantities that exist on the largest length
// scales, such as the terrain.
public abstract class biome
{
    // The size of a biome in meters, is
    // also the resolution of the point array
    // which defines the biome.
    public const int SIZE = 256;

    // The minimum and maximum fraction of the
    // biome edge that is used for blending into
    // adjacent biomes.
    public const float MIN_BLEND_FRAC = 0.25f;
    public const float MAX_BLEND_FRAC = 0.5f;

    public int x { get; private set; }
    public int z { get; private set; }

    // Coordinate transforms 
    protected float grid_to_world_x(int i) { return x * SIZE + i - SIZE / 2; }
    protected float grid_to_world_z(int j) { return z * SIZE + j - SIZE / 2; }

    protected abstract void generate_grid();

    public biome(int x, int z)
    {
        this.x = x;
        this.z = z;
        grid = new point[SIZE, SIZE];
        generate_grid();
    }

    public void destroy()
    {

    }

    // A particular point in the biome has these
    // properties. They are either loaded from disk,
    // or generated.
    public class point
    {
        public float altitude = 0;
        public float fertility = 1.0f;
        public Color terrain_color = new Color(0.4f, 0.6f, 0.2f, 0);

        public static point average(List<point> pts, List<float> wts)
        {
            point ret = new point();
            ret.altitude = 0;
            ret.fertility = 0;
            ret.terrain_color = new Color(0, 0, 0, 0);

            float total_weight = 0;
            foreach (var f in wts) total_weight += f;
            for (int i = 0; i < wts.Count; ++i)
            {
                float w = wts[i] / total_weight;
                var p = pts[i];
                ret.altitude += p.altitude * w;
                ret.fertility += p.fertility * w;
                ret.terrain_color.a += p.terrain_color.a * w;
                ret.terrain_color.r += p.terrain_color.r * w;
                ret.terrain_color.g += p.terrain_color.g * w;
                ret.terrain_color.b += p.terrain_color.b * w;
            }

            return ret;
        }
    }

    // The grid of points defining the biome
    protected point[,] grid;

    // Get a particular point in the biome in world
    // coordinates. Clamps the biome point values
    // outside the range of the biome.
    public point get_point(int world_x, int world_z)
    {
        int biome_x = world_x - x * SIZE + SIZE / 2;
        int biome_z = world_z - z * SIZE + SIZE / 2;
        if (biome_x < 0) biome_x = 0;
        if (biome_z < 0) biome_z = 0;
        if (biome_x >= SIZE) biome_x = SIZE - 1;
        if (biome_z >= SIZE) biome_z = SIZE - 1;
        return grid[biome_x, biome_z];
    }

    // Constructors for all biome types and a corresponding
    // random number in [0,1] for each
    static List<ConstructorInfo> biome_constructors = null;
    static List<float> biome_offsets = null;

    // Return the biome at the given biome coordinates
    public static biome at(int xb, int zb)
    {
        if (biome_constructors == null)
        {
            // Find the biome types
            biome_constructors = new List<ConstructorInfo>();
            biome_offsets = new List<float>();
            var asem = Assembly.GetAssembly(typeof(biome));
            var types = asem.GetTypes();

            float offset = 0f;
            foreach (var t in types)
            {
                // Check if this type is a valid biome
                if (!t.IsSubclassOf(typeof(biome))) continue;
                if (t.IsAbstract) continue;

                // Compile the list of biome constructors
                var c = t.GetConstructor(new System.Type[]{
                    typeof(int), typeof(int)
                });
                biome_constructors.Add(c);
                biome_offsets.Add(offset);

                // Offset each biome by an extra number that
                // isn't comensurate with 1.0, so generator
                // doesnt' repeat.
                offset += Mathf.PI;
            }
        }

        // Map chunk coordinates onto real-world coordinates
        // so perlin noise is generated in real world units
        float x = (float)(xb - 0.5f) * SIZE;
        float z = (float)(zb - 0.5f) * SIZE;

        // Sample perlin noise amounts for each biome and generate
        // the biome with the maximum value
        float max_perl = float.MinValue;
        int max_index = 0;
        for (int i = 0; i < biome_constructors.Count; ++i)
        {
            float r = biome_offsets[i];
            float p = Mathf.PerlinNoise(x / SIZE + r, z / SIZE - r);
            if (p > max_perl)
            {
                max_perl = p;
                max_index = i;
            }
        }
        return (biome)biome_constructors[max_index].Invoke(
            new object[] { xb, zb }
        );
    }
}

public class grass_islands : biome
{
    public const float HILL_SIZE = 27.2f;

    public grass_islands(int x, int z) : base(x, z) { }

    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                point p = new point();
                float xf = grid_to_world_x(i);
                float zf = grid_to_world_z(j);
                p.altitude = HILL_SIZE *
                    Mathf.PerlinNoise(xf / HILL_SIZE, zf / HILL_SIZE);
                grid[i, j] = p;
            }
    }
}

public class ocean : biome
{
    public ocean(int x, int z) : base(x, z) { }

    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                point p = new point();
                p.terrain_color = new Color(0.8f, 0.8f, 0f, 0f);
                grid[i, j] = p;
            }
    }
}

public class mountains : biome
{
    public const float MOUNTAIN_SIZE = 134.5f;
    public const float HILL_SIZE = 34.4f;
    public const float CLIFF_PERIOD = 54.2f;
    public const float MAX_CLIFF_HEIGHT = 5f;

    public mountains(int x, int z) : base(x, z) { }

    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                float xf = grid_to_world_x(i);
                float zf = grid_to_world_z(j);

                point p = new point();
                p.terrain_color = terrain_color(xf, zf);
                p.altitude = altitude(xf, zf);
                grid[i, j] = p;
            }
    }


    Color terrain_color(float x, float z)
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

    float altitude(float x, float z)
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