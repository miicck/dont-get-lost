using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mangroves : biome
{
    public const float ISLAND_SIZE = 27.2f;
    public const float MANGROVE_START_ALT = world.SEA_LEVEL - 3;
    public const float MANGROVE_DECAY_ALT = 3f;
    public const float MANGROVE_PROB = 0.15f;
    public const float BUSH_PROB = 0.2f;

    protected override void generate_grid()
    {
        // A random offset for the perlin noise generator
        float xrand = random.range(0, 1f);
        float zrand = random.range(0, 1f);

        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                var p = new point();

                // Alititude is simple perlin noise
                p.altitude = ISLAND_SIZE * Mathf.PerlinNoise(
                    xrand + i / ISLAND_SIZE, zrand + j / ISLAND_SIZE);
                p.terrain_color = terrain_colors.grass;

                // Work out mangrove amount, starts slightly below sea
                // level so mangroves go into water
                float man_amt = 0;
                if (p.altitude > MANGROVE_START_ALT)
                    man_amt = Mathf.Exp(
                        -(p.altitude - MANGROVE_START_ALT) / MANGROVE_DECAY_ALT);

                // Generate mangroves
                if (random.range(0, 1f) < man_amt * MANGROVE_PROB)
                    p.object_to_generate = world_object.load("mangroves");

                // Generate bushes
                else if (random.range(0, 1f) < man_amt * BUSH_PROB)
                    p.object_to_generate = world_object.load("bush");

                // Generate flowers
                else if (random.range(0, 25) == 0)
                    p.object_to_generate = world_object.load("flowers");

                // Generate mossy logs
                else if (random.range(0, 500) == 0)
                    p.object_to_generate = world_object.load("mossy_log");

                grid[i, j] = p;
            }
    }
}

public class desert : biome
{
    const float DUNE_DENSITY = 0.0008f;
    const float MAX_DUNE_HEIGHT = 16f;
    const float ROCKYNESS_DECAY_HEIGHT = MAX_DUNE_HEIGHT / 8f;
    const int MIN_DUNE_FOOTPRINT = 64;
    const int MAX_DUNE_FOOTPRINT = 128;
    const float LARGE_ROCKS_START = 0.8f;
    const float SMALL_ROCKS_START = 0.5f;

    protected override void generate_grid()
    {
        float[,] alt = new float[SIZE, SIZE];

        // Add a bunch of sand dunes to create the terrain
        const int DUNE_COUNT = (int)(DUNE_DENSITY * SIZE * SIZE);
        for (int i = 0; i < DUNE_COUNT; ++i)
        {
            int footprint = random.range(MIN_DUNE_FOOTPRINT, MAX_DUNE_FOOTPRINT);
            int x = random.range(0, SIZE);
            int z = random.range(0, SIZE);
            procmath.float_2D_tools.add_sand_dune(ref alt, x, z, footprint, footprint / 4);
        }
        procmath.float_2D_tools.rescale(ref alt, 0, MAX_DUNE_HEIGHT);

        // Small rocks to generate, in order of size
        world_object[] small_rocks = new world_object[]{
            world_object.load("desert_rocks_1"),
            world_object.load("desert_rocks_2"),
            world_object.load("desert_rocks_3"),
        };

        // Large rocks to generate, in order of size
        world_object[] large_rocks = new world_object[]{
            world_object.load("desert_rocks_4"),
            world_object.load("desert_rocks_5"),
        };

        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                // No water => altitiude is above the stage where beaches end
                var p = new point();
                p.altitude = alt[i, j] + point.BEACH_END;

                // Rockyness amount decreases with altitiude
                float rockyness = Mathf.Exp(-alt[i, j] / ROCKYNESS_DECAY_HEIGHT);
                p.terrain_color = Color.Lerp(terrain_colors.sand_dune, terrain_colors.desert_rock, rockyness);

                // No rocks => generate general desert foliage
                if (rockyness < SMALL_ROCKS_START)
                {
                    if (random.range(0, 200) == 0)
                        p.object_to_generate = world_object.load("cactus1");
                }

                // IF mediumly rocky, generate small rocks on a well-spaced grid
                else if (rockyness < LARGE_ROCKS_START && i % 8 == 0 && j % 8 == 0)
                {
                    float scale = (rockyness - SMALL_ROCKS_START) / (1.0f - SMALL_ROCKS_START);
                    p.object_to_generate = procmath.sliding_scale(scale, small_rocks, random);
                }

                // If very rocky, generate large rocks on a well-spaced grid
                else if (i % 10 == 0 && j % 10 == 0)
                {
                    if (random.range(0, 4) == 0)
                    {
                        float scale = (rockyness - LARGE_ROCKS_START) / (1.0f - LARGE_ROCKS_START);
                        p.object_to_generate = procmath.sliding_scale(scale, large_rocks, random);
                    }
                }

                grid[i, j] = p;
            }
    }
}

public class ocean : biome
{
    const int MAX_ISLAND_ALT = 8; // The maximum altitude of an island above sea level
    const int ISLAND_PERIOD = 32; // The range over which an island extends on the seabed
    const int MIN_ISLANDS = 1;    // Min number of islands
    const int MAX_ISLANDS = 3;    // Max number of islands

    protected override void generate_grid()
    {
        // Generate the altitude
        float[,] alt = new float[SIZE, SIZE];

        // Start with some perlin noise
        float xrand = random.range(0, 1f);
        float zrand = random.range(0, 1f);
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
                alt[i, j] += 0.5f * world.SEA_LEVEL * Mathf.PerlinNoise(
                    xrand + x / 16f, zrand + z / 16f);

        // Add a bunch of guassians to create desert islands
        // (also reduce the amount of perlin noise far from the islands
        //  to create a smooth seabed)
        int islands = random.range(MIN_ISLANDS, MAX_ISLANDS);
        for (int n = 0; n < islands; ++n)
            procmath.float_2D_tools.apply_guassian(ref alt,
                random.range(ISLAND_PERIOD, SIZE - ISLAND_PERIOD),
                random.range(ISLAND_PERIOD, SIZE - ISLAND_PERIOD),
                ISLAND_PERIOD, (f, g) =>
                    f * (0.5f + 0.5f * g) + // Reduced perlin noise
                    g * (world.SEA_LEVEL / 2f + MAX_ISLAND_ALT) // Added guassian
                );

        // Generate the point grid
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                point p = new point
                {
                    terrain_color = terrain_colors.sand,
                    altitude = alt[i, j]
                };

                if (p.altitude > world.SEA_LEVEL)
                    if (random.range(0, 100) == 0)
                        p.object_to_generate = world_object.load("palm_tree");

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

    protected override void generate_grid()
    {
        var alt = new float[SIZE, SIZE];

        // Generate altitiude by adding a bunch of randomly
        // rotated pyramids together
        const int MOUNTAIN_COUNT = (int)(MOUNTAIN_DENSITY * SIZE * SIZE);
        for (int n = 0; n < MOUNTAIN_COUNT; ++n)
        {
            int xm = random.Next() % SIZE;
            int zm = random.Next() % SIZE;
            int width = random.range(MIN_MOUNTAIN_WIDTH, MAX_MOUNTAIN_WIDTH);
            float rot = random.range(0, 360f);
            procmath.float_2D_tools.add_pyramid(ref alt, xm, zm, width, 1, rot);
        }
        procmath.float_2D_tools.rescale(ref alt, MIN_MOUNTAIN_HEIGHT, MAX_MOUNTAIN_HEIGHT);

        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                point p = new point
                {
                    altitude = alt[i, j],
                    terrain_color = terrain_colors.grass
                };

                // Choose terrain color based on altitiude
                if (p.altitude > SNOW_START)
                {
                    float s = procmath.maps.linear_turn_on(p.altitude, SNOW_START, SNOW_END);
                    p.terrain_color = Color.Lerp(terrain_colors.rock, terrain_colors.snow, s);
                }
                else if (p.altitude > ROCK_START)
                {
                    float r = procmath.maps.linear_turn_on(p.altitude, ROCK_START, ROCK_END);
                    p.terrain_color = Color.Lerp(terrain_colors.grass, terrain_colors.rock, r);
                }

                // Generate these objects between sea-level and rock-level
                if (p.altitude < ROCK_START &&
                    p.altitude > world.SEA_LEVEL)
                {
                    // Generate pine trees on a well-spaced grid
                    if (i % 3 == 0 && j % 3 == 0 && random.range(0, 4) == 0)
                        p.object_to_generate = world_object.load("pine_tree");

                    else if (random.range(0, 50) == 0)
                        p.object_to_generate = world_object.load("flowers");

                    else if (random.range(0, 500) == 0)
                        p.object_to_generate = world_object.load("mossy_log");
                }

                grid[i, j] = p;
            }
    }
}

public class flat_forest : biome
{
    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                var p = new biome.point();
                p.altitude = point.BEACH_END;
                p.terrain_color = terrain_colors.grass;
                if (random.range(0, 100) == 0)
                    p.object_to_generate = world_object.load("tree");
                else if (random.range(0, 40) == 0)
                    p.object_to_generate = world_object.load("flowers");
                else if (random.range(0, 150) == 0)
                    p.object_to_generate = world_object.load("mossy_log");
                else if (random.range(0, 200) == 0)
                    p.object_to_generate = world_object.load("bush");
                else if (random.range(0, 1000) == 0)
                    p.character_to_generate = character.load("chicken");

                grid[i, j] = p;
            }
    }
}

public class farmland : biome
{
    /// <summary> The smallest alllowed field edge size. </summary>
    const int MIN_FIELD_SIZE = 16;

    /// <summary> Fields larger than this will be subdivided. Must
    /// larger than 2 * MIN_FIELD_SIZE.</summary>
    const int MAX_FIELD_SIZE = MIN_FIELD_SIZE * 4;

    /// <summary> How wide the grass margin is
    /// at the edge of the field. </summary>
    const int MARGIN_WIDTH = 6;

    struct field
    {
        public enum TYPE
        {
            GRASS,
            LETTUCE,
            POTATO,
            APPLE,
            FARMYARD,
            WOODLAND,
        }

        public field(biome b, int left, int right, int bottom, int top)
        {
            int length = System.Enum.GetNames(typeof(TYPE)).Length;
            type = (TYPE)b.random.range(0, length);

            this.left = left;
            this.right = right;
            this.bottom = bottom;
            this.top = top;
        }

        public TYPE type { get; private set; }
        public int left { get; private set; }
        public int bottom { get; private set; }
        public int right { get; private set; }
        public int top { get; private set; }
        public int width { get => right - left; }
        public int height { get => top - bottom; }

        public bool is_margin(int x, int z)
        {
            return x > right - MARGIN_WIDTH ||
                   x < MARGIN_WIDTH ||
                   z > top - MARGIN_WIDTH ||
                   z < MARGIN_WIDTH;
        }

        public bool is_edge(int x, int z)
        {
            return x == left ||
                   z == bottom ||
                   x == right - 1 ||
                   z == top - 1;
        }
    }

    HashSet<field> fields;

    protected override void generate_grid()
    {
        // Use a divisive algorithm to split the area into
        // fields in a grid-like arrangement.

        // Start with a field covering the whole biome
        fields = new HashSet<field> { new field(this, 0, SIZE, 0, SIZE) };

        while (true)
        {
            // Set to true if a field was subdivided
            bool created = false;

            foreach (var f in new HashSet<field>(fields))
            {
                if (f.width > MAX_FIELD_SIZE)
                {
                    // Split field to reduce width
                    created = true;
                    fields.Remove(f);

                    int split = random.range(f.left + MIN_FIELD_SIZE, f.right - MIN_FIELD_SIZE);

                    fields.Add(new field(this, f.left, split, f.bottom, f.top));
                    fields.Add(new field(this, split, f.right, f.bottom, f.top));
                }
                else if (f.height > MAX_FIELD_SIZE)
                {
                    // Split field to reduce height
                    created = true;
                    fields.Remove(f);

                    int split = random.range(f.bottom + MIN_FIELD_SIZE, f.top - MIN_FIELD_SIZE);

                    fields.Add(new field(this, f.left, f.right, f.bottom, split));
                    fields.Add(new field(this, f.left, f.right, split, f.top));
                }
            }

            // No fields subdivided => done
            if (!created)
                break;
        }

        foreach (var f in fields)
        {
            for (int x = f.left; x < f.right; ++x)
                for (int z = f.bottom; z < f.top; ++z)
                {
                    var p = new point();
                    p.altitude = point.BEACH_END;

                    switch (f.type)
                    {
                        case field.TYPE.GRASS:
                        case field.TYPE.POTATO:
                        case field.TYPE.LETTUCE:
                        case field.TYPE.APPLE:

                            if (f.is_margin(x, z))
                            {
                                // Margin area
                                p.terrain_color = terrain_colors.grass;
                                p.altitude += 0.25f;

                                if (f.is_edge(x, z) && random.range(0, 4) != 0)
                                {
                                    // Hedgerow
                                    if (random.range(0, 30) == 0)
                                    {
                                       // p.object_to_generate = world_object.load("tree");
                                    }
                                    else
                                    {
                                        p.object_to_generate = world_object.load("hedge");
                                    }
                                }
                            }
                            else
                            {
                                // Field
                                switch (f.type)
                                {
                                    case field.TYPE.GRASS:
                                        p.terrain_color = terrain_colors.grass;
                                        break;

                                    case field.TYPE.LETTUCE:
                                        p.terrain_color = terrain_colors.dirt;
                                        if (x % 2 == 0 && z % 4 == 0)
                                            p.object_to_generate = world_object.load("lettuce");
                                        break;

                                    case field.TYPE.POTATO:
                                        p.terrain_color = terrain_colors.dirt;
                                        if (x % 4 == 0 && z % 2 == 0)
                                            p.object_to_generate = world_object.load("potato_plant");
                                        break;

                                    case field.TYPE.APPLE:
                                        p.terrain_color = terrain_colors.grass;
                                        if (x % 5 == 0 && z % 5 == 0)
                                            p.object_to_generate = world_object.load("apple_tree");
                                        else if (random.range(0, 50) == 0)
                                            p.object_to_generate = world_object.load("flowers");
                                        break;

                                    default:
                                        throw new System.Exception("Unkown crop!");
                                }

                            }

                            break;

                        case field.TYPE.WOODLAND:

                            p.terrain_color = terrain_colors.grass;
                            if (random.range(0, 100) == 0)
                                p.object_to_generate = world_object.load("bush");

                            break;


                        case field.TYPE.FARMYARD:

                            p.terrain_color = terrain_colors.dirt;
                            if (x == f.left && z == f.bottom)
                            {
                                p.object_to_generate = world_object.load("farmyard");
                            }
                            break;

                        default:
                            throw new System.Exception("Unkown field type!");
                    }

                    grid[x, z] = p;
                }
        }
    }
}

[biome_info(generation_enabled: false)]
public class cubes : biome
{
    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                var p = new point();
                p.altitude = 32f * Mathf.PerlinNoise(i / 64f, j / 64f);
                p.terrain_color = terrain_colors.grass;

                if (random.range(0, 100) == 0)
                    p.object_to_generate = world_object.load("rock_pillar");
                else if (random.range(0, 100) == 0)
                    p.object_to_generate = world_object.load("pentagon");
                grid[i, j] = p;
            }
    }
}

[biome_info(generation_enabled: false)]
public class canyon : biome
{
    const float CANYON_OUTER_WIDTH = 64f;
    const float CANYON_INNER_WIDTH = CANYON_OUTER_WIDTH - 16f;
    const float CANYON_DEPTH = 14f;
    const float RIVER_DEPTH = 2 * (point.BEACH_END - world.SEA_LEVEL);
    const float RIVER_WIDTH = 4f;
    const float CANYON_AMPLITUDE = 32f;
    const float CANYON_PERIOD = 64f;

    float canyon_profile(float dx)
    {
        float abs_dx = 2 * Mathf.Abs(dx);
        if (abs_dx > CANYON_OUTER_WIDTH) return 1f;
        if (abs_dx < CANYON_INNER_WIDTH) return 0f;
        return (abs_dx - CANYON_INNER_WIDTH) / (CANYON_OUTER_WIDTH - CANYON_INNER_WIDTH);
    }

    float river_profile(float dx)
    {
        return Mathf.Exp(-dx * dx / (2 * RIVER_WIDTH * RIVER_WIDTH));
    }

    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                float icentre = SIZE / 2 + CANYON_AMPLITUDE * Mathf.Sin(j / CANYON_PERIOD);
                float jcentre = SIZE / 2 + CANYON_AMPLITUDE * Mathf.Cos(i / CANYON_PERIOD);

                float cp1 = canyon_profile(i - icentre);
                float cp2 = canyon_profile(j - jcentre);

                float rp1 = river_profile(i - icentre);
                float rp2 = river_profile(j - jcentre);

                float cp = Mathf.Min(cp1, cp2);
                float rp = Mathf.Max(rp1, rp2);

                var p = new point();

                float CANYON_BED = point.BEACH_END;

                p.altitude = CANYON_BED + CANYON_DEPTH * cp;
                p.altitude -= RIVER_DEPTH * rp;
                p.terrain_color = terrain_colors.desert_rock;

                if (p.altitude < CANYON_BED + CANYON_DEPTH / 2f &&
                    p.altitude > CANYON_BED + 0.0001f)
                {
                    switch (random.range(0, 50))
                    {
                        case 0:
                            p.object_to_generate = world_object.load("desert_rocks_3");
                            break;
                        case 1:
                            p.object_to_generate = world_object.load("desert_rocks_4");
                            break;
                    }
                }

                grid[i, j] = p;
            }
    }
}

[biome_info(generation_enabled: false)]
public class desert_rocks : biome
{
    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                var p = new point();
                p.terrain_color = terrain_colors.desert_rock;

                float j_amp = 32f;
                float i_period = 64f;
                float width = 20f;

                float j_centre = SIZE / 2 + j_amp * Mathf.Sin(i / i_period);
                float dj = j - j_centre;
                float amt = Mathf.Exp(-dj * dj / (2f * width * width));

                p.altitude = point.BEACH_END;// + 32f - 32f * amt;

                if (random.range(0f, 1f) < amt)
                    switch (random.range(0, 200))
                    {
                        case 0:
                            p.object_to_generate = world_object.load("desert_rocks_3");
                            break;
                        case 1:
                            p.object_to_generate = world_object.load("desert_rocks_4");
                            break;
                    }

                grid[i, j] = p;
            }
    }
}

[biome_info(generation_enabled: false)]
public class terraced_hills : biome
{
    public const float HILL_HEIGHT = 50f;
    public const float HILL_SIZE = 64f;

    protected override void generate_grid()
    {
        float xrand = random.range(0, 1f);
        float zrand = random.range(0, 1f);
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                point p = new point
                {
                    altitude = HILL_HEIGHT * Mathf.PerlinNoise(
                        xrand + i / HILL_SIZE, zrand + j / HILL_SIZE),
                    terrain_color = terrain_colors.grass
                };

                if (p.altitude < HILL_HEIGHT / 2)
                {
                    if ((i % 25 == 0) && (j % 25 == 0))
                        p.object_to_generate = world_object.load("flat_outcrop_large");
                }
                else if (random.range(0, 200) == 0)
                    p.object_to_generate = world_object.load("tree");

                grid[i, j] = p;
            }
    }
}

[biome_info(generation_enabled: false)]
public class cliffs : biome
{
    public const float CLIFF_HEIGHT = 10f;
    public const float CLIFF_PERIOD = 32f;

    public const float HILL_HEIGHT = 52f;
    public const float HILL_PERIOD = 64f;

    protected override void generate_grid()
    {
        // Generate the altitudes
        float xrand = random.range(0, 1f);
        float zrand = random.range(0, 1f);
        float[,] alt = new float[SIZE, SIZE];
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                // Work out how much cliff there is
                float cliffness = Mathf.PerlinNoise(
                    xrand + i / CLIFF_PERIOD, zrand + j / CLIFF_PERIOD);
                if (cliffness > 0.5f) cliffness = 1;
                else cliffness *= 2;

                // Work out the smooth altitude
                float asmooth = HILL_HEIGHT * Mathf.PerlinNoise(
                    xrand + i / HILL_PERIOD, zrand + j / HILL_PERIOD);

                // Work out the cliffy altitdue
                float acliff = CLIFF_HEIGHT *
                    procmath.maps.smoothed_floor(asmooth / CLIFF_HEIGHT, 0.75f);

                // Mix them
                alt[i, j] = asmooth * (1 - cliffness) + acliff * cliffness;
            }

        var grad = procmath.float_2D_tools.get_gradients(alt);
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                float dirt_amt = grad[i, j].magnitude;
                dirt_amt = procmath.maps.linear_turn_on(dirt_amt, 0.2f, 0.5f);

                grid[i, j] = new point()
                {
                    altitude = alt[i, j],
                    terrain_color = Color.Lerp(terrain_colors.grass, terrain_colors.dirt, dirt_amt)
                };
            }
    }
}