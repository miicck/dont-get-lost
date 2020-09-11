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
                p.sky_color = sky_colors.light_blue;
                p.fog_distance = fog_distances.CLOSE;

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

                // On the beach
                else if (p.altitude < point.BEACH_END && p.altitude > world.SEA_LEVEL)
                {
                    // Generate rocks
                    if (random.range(0, 100) == 0)
                        p.object_to_generate = world_object.load("flint");
                    else if (random.range(0, 100) == 0)
                        p.object_to_generate = world_object.load("iron_ore");
                    else if (random.range(0, 200) == 0)
                        p.object_to_generate = world_object.load("flat_rock_outcrop");
                }

                // On land
                else if (p.altitude > world.SEA_LEVEL)
                {
                    // Generate flowers
                    if (random.range(0, 25) == 0)
                        p.object_to_generate = world_object.load("flowers");

                    // Generate mossy logs
                    else if (random.range(0, 500) == 0)
                        p.object_to_generate = world_object.load("mossy_log");

                    else if (random.range(0, 1000) == 0)
                        p.object_to_generate = world_object.load("chicken_nest");

                }

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

    void create_oasis(int x, int z, int scale)
    {
        // Ensure we're in range
        int x_min = x - scale / 2; if (x_min < 0) return;
        int x_max = x + scale / 2; if (x_max > SIZE) return;
        int z_min = z - scale / 2; if (z_min < 0) return;
        int z_max = z + scale / 2; if (z_max > SIZE) return;

        for (int i = x_min; i < x_max; ++i)
            for (int j = z_min; j < z_max; ++j)
            {
                // Remove all objects
                grid[i, j].object_to_generate = null;

                // Lower terrain
                float im = procmath.maps.maximum_in_middle(i, x_min, x_max);
                float jm = procmath.maps.maximum_in_middle(j, z_min, z_max);
                grid[i, j].altitude -= im * jm * 8f;

                if (grid[i, j].altitude < point.BEACH_END &&
                    grid[i, j].altitude > world.SEA_LEVEL)
                {
                    // On the beach
                    if (random.range(0, 50) == 0)
                        grid[i, j].object_to_generate = world_object.load("palm_tree");
                    else if (random.range(0, 50) == 0)
                        grid[i, j].object_to_generate = world_object.load("mossy_log");
                    else if (random.range(0, 100) == 0)
                        grid[i, j].object_to_generate = world_object.load("flint");
                }
                else if (grid[i, j].altitude < world.SEA_LEVEL)
                {
                    // Underwater
                    if (random.range(0, 100) == 0)
                        grid[i, j].object_to_generate = world_object.load("boulder");
                }
            }
    }

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
                p.sky_color = sky_colors.light_blue;
                p.fog_distance = fog_distances.OFF;

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

        for (int i = 0; i < 8; ++i)
            create_oasis(random.range(0, SIZE),
                         random.range(0, SIZE),
                         random.range(16, 32));
    }
}

public class ocean : biome
{
    public const float OCEAN_AMT = 0.7f;
    public const float ALT_SCALE = 16f;

    protected override void generate_grid()
    {
        const float ABOVE = ALT_SCALE * (1 - OCEAN_AMT);
        const float BELOW = ALT_SCALE * OCEAN_AMT;

        // Generate the point grid
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                float alt = Mathf.PerlinNoise(i / 64f, j / 64f);
                alt = world.SEA_LEVEL + alt * ABOVE - (1 - alt) * BELOW;

                point p = new point
                {
                    terrain_color = terrain_colors.sand,
                    sky_color = sky_colors.light_blue,
                    altitude = alt,
                    fog_distance = fog_distances.FAR
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

    void add_quarry(int x, int z, int scale)
    {
        int min_x = x - scale / 2; if (min_x < 0) return;
        int min_z = z - scale / 2; if (min_z < 0) return;
        int max_x = x + scale / 2; if (max_x > SIZE) return;
        int max_z = z + scale / 2; if (max_z > SIZE) return;

        // Work out the average altitude of the quarry
        float average = 0;
        float count = 0;
        for (int i = min_x; i < max_x; ++i)
            for (int j = min_z; j < max_z; ++j)
            {
                average += grid[i, j].altitude;
                count += 1;
            }
        average /= count;

        for (int i = min_x; i < max_x; ++i)
            for (int j = min_z; j < max_z; ++j)
            {
                var it = procmath.maps.trapesium(i, min_x, max_x, scale / 4);
                var jt = procmath.maps.trapesium(j, min_z, max_z, scale / 4);
                float av_amt = it * jt;
                grid[i, j].altitude = grid[i, j].altitude * (1f - av_amt) + average * av_amt;
            }
    }

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
                    terrain_color = terrain_colors.grass,
                    sky_color = sky_colors.light_blue,
                    fog_distance = fog_distances.MEDIUM
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

                // Generate beach stuff
                if (p.altitude < point.BEACH_END && p.altitude > world.SEA_LEVEL)
                {
                    if (random.range(0, 10) == 0)
                        p.object_to_generate = world_object.load("slate");
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
                var p = new point();
                p.altitude = point.BEACH_END;
                p.terrain_color = terrain_colors.grass;
                p.sky_color = sky_colors.light_blue;
                p.fog_distance = fog_distances.MEDIUM;

                if (random.range(0, 100) == 0)
                    p.object_to_generate = world_object.load("tree");
                else if (random.range(0, 100) == 0)
                    p.object_to_generate = world_object.load("flowers");
                else if (random.range(0, 300) == 0)
                    p.object_to_generate = world_object.load("mossy_log");
                else if (random.range(0, 300) == 0)
                    p.object_to_generate = world_object.load("bush");
                else if (random.range(0, 400) == 0)
                    p.object_to_generate = world_object.load("flint");
                else if (random.range(0, 1500) == 0)
                    p.object_to_generate = world_object.load("chicken_nest");
                else if (random.range(0, 2000) == 0)
                    p.object_to_generate = world_object.load("jutting_rock");

                grid[i, j] = p;
            }
    }
}

public class charred_forest : biome
{
    public const float HILL_HEIGHT = 4f;
    public const float HILL_PERIOD = 20f;
    public const int SPIDER_GRID_SPACING = 64;
    public const float SPIDER_HILL_HEIGHT = 8f;
    public const int SPIDER_HILL_PERIOD = 24;
    public const float SPIDER_HILL_MOD_PERIOD = 12f;

    protected override void generate_grid()
    {
        float[,] spideryness = new float[SIZE, SIZE];

        HashSet<int> x_spawners = new HashSet<int>();
        HashSet<int> z_spawners = new HashSet<int>();

        for (int i = SPIDER_GRID_SPACING / 2;
            i < SIZE - SPIDER_GRID_SPACING / 2;
            i += SPIDER_GRID_SPACING)
            for (int j = SPIDER_GRID_SPACING / 2;
                j < SIZE - SPIDER_GRID_SPACING / 2;
                j += SPIDER_GRID_SPACING)
            {
                int x = i + random.range(0, SPIDER_GRID_SPACING / 2);
                int z = j + random.range(0, SPIDER_GRID_SPACING / 2);
                x_spawners.Add(x);
                z_spawners.Add(z);

                procmath.float_2D_tools.add_smooth_dome(
                    ref spideryness, x, z, SPIDER_HILL_PERIOD, 1f);
            }

        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                var p = new point();
                float s = spideryness[i, j];

                float altitude = HILL_HEIGHT * Mathf.PerlinNoise(
                    i / HILL_PERIOD, j / HILL_PERIOD);

                if (s > 10e-4)
                {
                    altitude += s * SPIDER_HILL_HEIGHT * Mathf.PerlinNoise(
                        i / SPIDER_HILL_MOD_PERIOD, j / SPIDER_HILL_MOD_PERIOD);

                    bool spawner = s > 0.25f && i % 4 == 0 && j % 4 == 0 &&
                        random.range(0, 8) == 0;

                    if (x_spawners.Contains(i) && z_spawners.Contains(j))
                        spawner = true;

                    if (spawner)
                        p.object_to_generate = world_object.load("smoke_spider_nest");
                    else if (random.range(0, 10) == 0)
                        p.object_to_generate = world_object.load("charred_rock");
                }
                else
                {
                    if (random.range(0, 100) == 0)
                        p.object_to_generate = world_object.load("charred_tree_stump");
                }

                p.altitude = point.BEACH_END + altitude;
                p.terrain_color = terrain_colors.charred_earth;
                p.sky_color = sky_colors.smoke_grey;
                p.fog_distance = fog_distances.VERY_CLOSE;

                grid[i, j] = p;
            }
    }
}

public class ice_ocean : biome
{
    public const float OSCILLATION_PERIOD = 64f;
    public const float OSCILLATION_AMP = 16f;

    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                var p = new point();
                p.terrain_color = terrain_colors.snow;
                p.sky_color = sky_colors.light_blue;
                p.altitude = 0;
                p.fog_distance = fog_distances.OFF;

                if (random.range(0, 400) == 0)
                    p.object_to_generate = world_object.load("ice_sheet");
                else if (random.range(0, 400) == 0)
                    p.object_to_generate = world_object.load("iceberg");

                grid[i, j] = p;
            }
    }
}

public class rock_stacks : biome
{
    public const float HILL_PERIOD = 64f;
    public const float HILL_SIZE = 32f;
    public const float WATER_FRAC = 0.25f;
    public const float WIGGLE_PERIOD = 16f;
    public const float WIGGLE_AMP = HILL_SIZE;

    protected override void generate_grid()
    {
        // Derived constants
        const float HILL_ABOVE = HILL_SIZE * (1 - WATER_FRAC);
        const float HILL_BELOW = HILL_SIZE * WATER_FRAC;
        const float ROCK_THRESH_ALT = world.SEA_LEVEL + HILL_ABOVE * 0.5f - HILL_BELOW;

        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                // Point setup
                var p = grid[i, j] = new point();
                p.terrain_color = terrain_colors.grass;
                p.sky_color = sky_colors.light_blue;
                p.fog_distance = fog_distances.FAR;

                // Generate the altitude using some perlin noise (where the coordinates
                // have been wiggled with another perlin noise)
                float i_wiggle = i + WIGGLE_AMP * Mathf.PerlinNoise(i / WIGGLE_PERIOD, j / WIGGLE_PERIOD);
                float hill_amt = Mathf.PerlinNoise(i_wiggle / HILL_PERIOD, j / HILL_PERIOD);
                p.altitude = world.SEA_LEVEL + HILL_ABOVE * hill_amt - HILL_BELOW;

                if (p.altitude < world.SEA_LEVEL + 0.1f)
                {

                }
                else if (hill_amt > 0.3f)
                {
                    // Generate some well-spaced trees
                    if (i % 3 == 0 && j % 3 == 0 && random.range(0, 10) == 0)
                    {
                        p.object_to_generate = world_object.load("tree");

                        if (random.range(0, 5) == 0 && i > 2 && j > 2)
                            grid[i - 2, j - 2].object_to_generate = world_object.load("mossy_log");
                    }

                    // Generate bushes
                    else if (random.range(0, 200) == 0)
                        p.object_to_generate = world_object.load("bush");
                }
            }

        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                var p = grid[i, j];
                if (p.altitude > ROCK_THRESH_ALT)
                {
                    // Generate some well-spaced rocks
                    if (i % 5 == 0 && j % 5 == 0 && random.range(0, 8) == 0)
                    {
                        // Clear a space for the rocks
                        const int CLEAR_SIZE = 5;
                        for (int i2 = Mathf.Max(0, i - CLEAR_SIZE);
                                 i2 < Mathf.Min(SIZE, i + CLEAR_SIZE); ++i2)
                            for (int j2 = Mathf.Max(0, j - CLEAR_SIZE);
                                     j2 < Mathf.Min(SIZE, j + CLEAR_SIZE); ++j2)
                                grid[i2, j2].object_to_generate = null;

                        // Add the rocks
                        if (random.range(0, 2) == 0)
                            p.object_to_generate = world_object.load("rock_stacks_1");
                        else
                            p.object_to_generate = world_object.load("rock_plateau_1");
                    }
                }
            }
    }
}

public class jungle : biome
{
    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                // Point setup
                var p = grid[i, j] = new point();
                p.terrain_color = terrain_colors.jungle_moss;
                p.sky_color = sky_colors.jungle_green;
                p.altitude = world.SEA_LEVEL + 16f * Mathf.PerlinNoise(i / 32f, j / 32f) - 8f;
                p.fog_distance = fog_distances.CLOSE;
                p.object_to_generate = get_object(p.altitude, random);
            }
    }

    public static world_object get_object(float altitude, System.Random random)
    {
        if (random.range(0, 400) == 0)
            return world_object.load("jungle_tree_1");

        if (random.range(0, 400) == 0)
            return world_object.load("jungle_tree_2");

        if (random.range(0, 50) == 0)
        {
            if (random.range(0, 2) == 0)
                return world_object.load("tree_fern_bent");
            else
                return world_object.load("tree_fern");
        }

        if (random.range(0, 100) == 0)
            return world_object.load("mossy_log_jungle");

        if (random.range(0, 400) == 0)
            return world_object.load("jungle_cliffs");

        if (altitude < point.BEACH_END && altitude > world.SEA_LEVEL)
        {
            // On the beach
            if (random.range(0, 100) == 0)
                return world_object.load("flint");

            if (random.range(0, 200) == 0)
                return world_object.load("flat_rock_outcrop");
        }

        return null;
    }
}

public class jungle_mountains : biome
{
    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                // Point setup
                var p = grid[i, j] = new point();
                p.terrain_color = terrain_colors.jungle_moss;
                p.sky_color = sky_colors.jungle_green;
                p.fog_distance = fog_distances.MEDIUM;

                float alt = Mathf.PerlinNoise(
                    i / 64f + x * 10f, j / 64f + z * 10f);

                alt = (alt - 0.5f) * 2f;
                if (alt < 0f) alt = 0f;
                alt = Mathf.Pow(alt, 0.5f);
                p.altitude = 64f * alt;

                p.object_to_generate = jungle.get_object(p.altitude, random);

                if (p.object_to_generate == null)
                {
                    if (p.altitude > world.SEA_LEVEL - 2 &&
                        p.altitude < point.BEACH_END)
                        if (i % 3 == 0 && j % 3 == 0)
                            p.object_to_generate = world_object.load("flat_rock_outcrop");
                }
            }
    }
}

public class grassy_tiers : biome
{
    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                // Point setup
                var p = grid[i, j] = new point();
                p.terrain_color = terrain_colors.grass;
                p.sky_color = sky_colors.light_blue;
                p.fog_distance = fog_distances.MEDIUM;

                p.altitude = Mathf.PerlinNoise(i / 32f, j / 32f);
                p.altitude *= 32f;

                if (i % 3 == 0 && j % 3 == 0 && random.range(0, 3) == 0)
                    p.object_to_generate = world_object.load("grassy_tier");
                else if (random.range(0, 300) == 0)
                    p.object_to_generate = world_object.load("tree");
            }
    }
}

public class swamp : biome
{
    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                // Point setup
                var p = grid[i, j] = new point();
                p.altitude = world.SEA_LEVEL + 0.1f - 0.5f * Mathf.PerlinNoise(i / 16f, j / 16f);
                p.water_color = water_colors.swampy_green;
                p.sky_color = sky_colors.jungle_green;
                p.fog_distance = fog_distances.VERY_CLOSE;

                if (random.range(0, 50) == 0)
                    p.object_to_generate = world_object.load("spiky_tree_stump");
                else if (random.range(0, 50) == 0)
                    p.object_to_generate = world_object.load("swampy_tree");
                else if (random.range(0, 10) == 0)
                    p.object_to_generate = world_object.load("lillypad");
            }
    }
}

public class caves : biome
{
    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                // Point setup
                var p = grid[i, j] = new point();
                p.altitude = world.SEA_LEVEL - 2f;
                p.sky_color = sky_colors.underground_darkness;
                p.fog_distance = fog_distances.CLOSE;

                if (i % 5 == 0 && j % 5 == 0 && random.range(0, 10) == 0)
                    p.object_to_generate = world_object.load("rock_pillar_1");
            }
    }
}

public class rocky_plains : biome
{
    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                // Point setup
                var p = grid[i, j] = new point();
                p.sky_color = sky_colors.light_blue;
                p.terrain_color = terrain_colors.grass;
                p.fog_distance = fog_distances.VERY_FAR;

                float alt = Mathf.PerlinNoise(i / 64f, j / 64f);
                if (alt < 0.3f) alt = (alt - 0.3f) * 4f;
                else if (alt < 0.5f) alt = 0f;
                else alt = procmath.maps.end_smoothed_linear(2 * (alt - 0.5f), 0.25f);
                p.altitude = point.BEACH_END + 16f * alt;

                float bush_amt = Mathf.PerlinNoise(i / 32f, j / 32f);

                if (alt < 1e-4)
                {
                    if (random.range(0, 800) == 0)
                        p.object_to_generate = world_object.load("flat_top_tree");
                    else if (random.range(0, 300) == 0)
                        p.object_to_generate = world_object.load("layered_hill");
                    else if (bush_amt > 0.7f && random.range(0, 5) == 0)
                    {
                        if (random.range(0, 4) == 0)
                            p.object_to_generate = world_object.load("rhododendron");
                        else
                            p.object_to_generate = world_object.load("bush");
                    }
                    else if (bush_amt > (0.7f + random.range(0, 0.3f)) && random.range(0, 4) == 0)
                        p.object_to_generate = world_object.load("foxglove");
                }
                else
                {
                    if (random.range(0, 400) == 0)
                        p.object_to_generate = world_object.load("layered_mountain");
                }
            }
    }
}

public class spikey_mountains : biome
{
    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                // Point setup
                var p = grid[i, j] = new point();

                p.altitude = Mathf.PerlinNoise(i / 64f + x, j / 64f + z);
                p.altitude = Mathf.Pow(p.altitude, 1.5f) * 64f;
                p.fog_distance = fog_distances.VERY_CLOSE;

                float tall_rock_density =
                    Mathf.PerlinNoise(i / 45f + 0.5f, j / 45f + 0.7f);

                if (tall_rock_density > 0.4f)
                {
                    // High density of tall rocks
                    if (i % 6 == 0 && j % 6 == 0)
                    {
                        if (random.range(0, 5) == 0)
                            p.object_to_generate = world_object.load("tall_rock");
                        else if (random.range(0, 5) == 0)
                            p.object_to_generate = world_object.load("tiered_mountiains");
                    }
                }
                else if (tall_rock_density > 0.3f)
                {
                    // Low density of tall rocks
                    if (i % 6 == 0 && j % 6 == 0)
                    {
                        if (random.range(0, 15) == 0)
                            p.object_to_generate = world_object.load("tall_rock");
                        else if (random.range(0, 15) == 0)
                            p.object_to_generate = world_object.load("tiered_mountiains");
                    }
                }
                else
                {
                    // No tall rocks
                    if (random.range(0, 25) == 0)
                        p.object_to_generate = world_object.load("cherry_blossom");
                    else if (random.range(0, 50) == 0)
                        p.object_to_generate = world_object.load("apple_tree");
                    else if (random.range(0, 100) == 0)
                        p.object_to_generate = world_object.load("bush");
                    else if (random.range(0, 100) == 0)
                        p.object_to_generate = world_object.load("flint");
                    else if (random.range(0, 100) == 0)
                        p.object_to_generate = world_object.load("mossy_log");
                }
            }
    }
}

[biome_info(generation_enabled: false)]
public class empty : biome
{
    protected override void generate_grid()
    {
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                // Point setup
                var p = grid[i, j] = new point();
                p.altitude = world.SEA_LEVEL + 1;
            }
    }
}
