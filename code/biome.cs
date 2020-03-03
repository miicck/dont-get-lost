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
    public virtual string suggest_object(int x, int z) { return null; }

    public string filename() { return world.save_folder() + "/biome_" + x + "_" + z; }

    public biome(int x, int z)
    {
        this.x = x;
        this.z = z;
        grid = new point[SIZE, SIZE];
        if (!load())
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            generate_grid();
            utils.log("Generated biome " + x + ", " + z + " (" + GetType().Name +
                ") in " + sw.ElapsedMilliseconds + " ms", "generation");
        }
    }

    public void destroy()
    {
        // Save the chunk to file
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using (var stream = new System.IO.FileStream(filename(), System.IO.FileMode.Create))
        {
            // Write the length of a point to the file
            var bytes_00 = grid[0, 0].serialize();
            int length = bytes_00.Length;
            var length_bytes = System.BitConverter.GetBytes(length);
            stream.Write(length_bytes, 0, length_bytes.Length);

            // Write the points to the file
            for (int i = 0; i < SIZE; ++i)
                for (int j = 0; j < SIZE; ++j)
                {
                    var bytes = grid[i, j].serialize();
                    stream.Write(bytes, 0, bytes.Length);
                }
        }
        utils.log("Saved biome " + x + ", " + z + " in " + sw.ElapsedMilliseconds + " ms", "io");
    }

    bool load()
    {
        // Read the chunk from file
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (!System.IO.File.Exists(filename())) return false;
        using (var stream = new System.IO.FileStream(filename(), System.IO.FileMode.Open, System.IO.FileAccess.Read))
        {
            // Read the length of a point from the start of the file
            byte[] legnth_bytes = new byte[sizeof(int)];
            stream.Read(legnth_bytes, 0, legnth_bytes.Length);
            int length = System.BitConverter.ToInt32(legnth_bytes, 0);

            byte[] bytes = new byte[length];
            for (int i = 0; i < SIZE; ++i)
                for (int j = 0; j < SIZE; ++j)
                {
                    stream.Read(bytes, 0, bytes.Length);
                    grid[i, j] = new point(bytes);
                }
        }
        utils.log("Loaded biome " + x + ", " + z + " in " + sw.ElapsedMilliseconds + " ms", "io");
        return true;
    }

    // A particular point in the biome has these
    // properties. They are either loaded from disk,
    // or generated.
    public struct point
    {
        public float altitude;
        public Color terrain_color;

        // Serialise a point into bytes
        public byte[] serialize()
        {
            float[] floats = new float[]
            {
                altitude, terrain_color.r, terrain_color.g, terrain_color.b
            };
            byte[] bytes = new byte[sizeof(float) * floats.Length];
            System.Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        // Construct a point from it's serialized floats
        public point(byte[] bytes)
        {
            altitude = System.BitConverter.ToSingle(bytes, 0);
            terrain_color.r = System.BitConverter.ToSingle(bytes, sizeof(float));
            terrain_color.g = System.BitConverter.ToSingle(bytes, 2 * sizeof(float));
            terrain_color.b = System.BitConverter.ToSingle(bytes, 3 * sizeof(float));
            terrain_color.a = 0;
        }

        // Computea a weighted average of a list of points
        public static point average(point[] pts, float[] wts)
        {
            point ret = new point
            {
                altitude = 0,
                terrain_color = new Color(0, 0, 0, 0)
            };

            float total_weight = 0;
            foreach (var f in wts) total_weight += f;
            for (int i = 0; i < wts.Length; ++i)
            {
                float w = wts[i] / total_weight;
                var p = pts[i];
                ret.altitude += p.altitude * w;
                ret.terrain_color.r += p.terrain_color.r * w;
                ret.terrain_color.g += p.terrain_color.g * w;
                ret.terrain_color.b += p.terrain_color.b * w;
            }

            return ret;
        }

        public void apply_global_rules()
        {
            // Enforce beach color
            const float SAND_START = world.SEA_LEVEL + 1f;
            const float SAND_END = world.SEA_LEVEL + 2f;

            if (altitude < SAND_START)
                terrain_color = colors.sand;
            else if (altitude < SAND_END)
            {
                float s = 1 - procmath.maps.linear_turn_on(altitude, SAND_START, SAND_END);
                terrain_color = Color.Lerp(terrain_color, colors.sand, s);
            }
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
    public static string biome_override = "";

    // Generate the biome at the given biome coordinates
    public static biome generate(int xb, int zb)
    {
        if (biome_constructors == null)
        {
            // Find the biome types
            biome_constructors = new List<ConstructorInfo>();
            biome_offsets = new List<float>();
            var asem = Assembly.GetAssembly(typeof(biome));
            var types = asem.GetTypes();

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

                // Offset each biome by a ranbdom amount
                // so generates differently each time
                biome_offsets.Add(Random.Range(0, 1f));

                if (t.Name == biome_override)
                {
                    // Enforce the biome override
                    biome_constructors = new List<ConstructorInfo> { c };
                    biome_offsets = new List<float> { 0 };
                    break;
                }
            }
        }

        // Map chunk coordinates onto real-world coordinates
        // so perlin noise is generated in real world units
        float x = (xb - 0.5f) * SIZE;
        float z = (zb - 0.5f) * SIZE;

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

        // Invoke the chosen biome constructor
        return (biome)biome_constructors[max_index].Invoke(
            new object[] { xb, zb }
        );
    }
}

public class grass_islands : biome
{
    public const float ISLAND_SIZE = 27.2f;

    public grass_islands(int x, int z) : base(x, z) { }

    public override string suggest_object(int x, int z)
    {
        return "tree1";
    }

    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                point p = new point();
                float xf = grid_to_world_x(i);
                float zf = grid_to_world_z(j);
                p.altitude = ISLAND_SIZE *
                    Mathf.PerlinNoise(xf / ISLAND_SIZE, zf / ISLAND_SIZE);
                p.terrain_color = colors.grass;
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
                point p = new point
                {
                    terrain_color = colors.sand
                };
                grid[i, j] = p;
            }
    }
}

public class mountains : biome
{
    const int MIN_MOUNTAIN_WIDTH = 64;
    const int MAX_MOUNTAIN_WIDTH = 128;
    const float MAX_MOUNTAIN_HEIGHT = 128f;
    const float MIN_MOUNTAIN_HEIGHT = 0f;
    const float SNOW_START = 80f;
    const float SNOW_END = 100f;
    const float ROCK_START = 50f;
    const float ROCK_END = 70f;
    const float MOUNTAIN_DENSITY = 0.0008f;

    public mountains(int x, int z) : base(x, z) { }

    protected override void generate_grid()
    {
        var alt = new float[SIZE, SIZE];

        const int MOUNTAIN_COUNT = (int)(MOUNTAIN_DENSITY * SIZE * SIZE);
        for (int n = 0; n < MOUNTAIN_COUNT; ++n)
        {
            int xm = Random.Range(0, SIZE);
            int zm = Random.Range(0, SIZE);
            int width = Random.Range(MIN_MOUNTAIN_WIDTH, MAX_MOUNTAIN_WIDTH);
            float rot = Random.Range(0, 360f);
            procmath.float_2D_tools.add_pyramid(ref alt, xm, zm, width, 1, rot);
        }

        procmath.float_2D_tools.rescale(ref alt, MIN_MOUNTAIN_HEIGHT, MAX_MOUNTAIN_HEIGHT);

        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                float xf = grid_to_world_x(i);
                float zf = grid_to_world_z(j);

                point p = new point
                {
                    altitude = alt[i, j],

                    terrain_color = colors.grass
                };
                if (p.altitude > SNOW_START)
                {
                    float s = procmath.maps.linear_turn_on(p.altitude, SNOW_START, SNOW_END);
                    p.terrain_color = Color.Lerp(colors.rock, colors.snow, s);
                }
                else if (p.altitude > ROCK_START)
                {
                    float r = procmath.maps.linear_turn_on(p.altitude, ROCK_START, ROCK_END);
                    p.terrain_color = Color.Lerp(colors.grass, colors.rock, r);
                }

                grid[i, j] = p;
            }
    }
}