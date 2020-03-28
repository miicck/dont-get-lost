using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class generated_biome : biome
{
    protected override bool load()
    {
        throw new System.Exception("Generated bioms should not call load()!");
    }
}

public class mangroves : generated_biome
{
    public const float ISLAND_SIZE = 27.2f;
    public const float MANGROVE_START_ALT = world.SEA_LEVEL - 3;
    public const float MANGROVE_DECAY_ALT = 3f;
    public const float MANGROVE_PROB = 0.2f;

    protected override void generate_grid()
    {
        float xrand = Random.Range(0, 1f);
        float zrand = Random.Range(0, 1f);

        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                var p = new point();

                p.altitude = ISLAND_SIZE * Mathf.PerlinNoise(
                    xrand + i / ISLAND_SIZE, zrand + j / ISLAND_SIZE);
                p.terrain_color = colors.grass;

                float man_amt = 0;
                if (p.altitude > MANGROVE_START_ALT)
                    man_amt = Mathf.Exp(
                        -(p.altitude - MANGROVE_START_ALT) / MANGROVE_DECAY_ALT);

                if (Random.Range(0, 1f) < man_amt * MANGROVE_PROB)
                    p.world_object_gen = new world_object_generator("mangroves");

                grid[i, j] = p;
            }
    }
}

public class ocean : generated_biome
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
        float xrand = Random.Range(0, 1f);
        float zrand = Random.Range(0, 1f);
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
                alt[i, j] += 0.5f * world.SEA_LEVEL * Mathf.PerlinNoise(
                    xrand + x / 16f, zrand + z / 16f);

        // Add a bunch of guassians to create desert islands
        // (also reduce the amount of perlin noise far from the islands
        //  to create a smooth seabed)
        int islands = Random.Range(MIN_ISLANDS, MAX_ISLANDS);
        for (int n = 0; n < islands; ++n)
            procmath.float_2D_tools.apply_guassian(ref alt,
                Random.Range(ISLAND_PERIOD, SIZE - ISLAND_PERIOD),
                Random.Range(ISLAND_PERIOD, SIZE - ISLAND_PERIOD),
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
                    terrain_color = colors.sand,
                    altitude = alt[i, j]
                };

                if (p.altitude > world.SEA_LEVEL)
                    if (Random.Range(0, 100) == 0)
                        p.world_object_gen = new world_object_generator("palm_tree");

                grid[i, j] = p;
            }
    }
}

public class mountains : generated_biome
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

                if (p.altitude < ROCK_START &&
                    p.altitude > world.SEA_LEVEL)
                    if (Random.Range(0, 40) == 0)
                        p.world_object_gen = new world_object_generator("pine_tree");

                grid[i, j] = p;
            }
    }
}

public class terraced_hills : generated_biome
{
    public const float HILL_HEIGHT = 50f;
    public const float HILL_SIZE = 64f;

    protected override void generate_grid()
    {
        float xrand = Random.Range(0, 1f);
        float zrand = Random.Range(0, 1f);
        for (int i = 0; i < SIZE; ++i)
            for (int j = 0; j < SIZE; ++j)
            {
                point p = new point
                {
                    altitude = HILL_HEIGHT * Mathf.PerlinNoise(
                        xrand + i / HILL_SIZE, zrand + j / HILL_SIZE),
                    terrain_color = colors.grass
                };

                if (p.altitude < HILL_HEIGHT / 2)
                {
                    if ((i % 25 == 0) && (j % 25 == 0))
                    {
                        p.world_object_gen = new world_object_generator("flat_outcrop");
                        p.world_object_gen.additional_scale_factor = 4f;
                    }
                }
                else if (Random.Range(0, 200) == 0)
                    p.world_object_gen = new world_object_generator("tree");

                grid[i, j] = p;
            }
    }
}

[biome_info(generation_enabled: false)]
public class cliffs : generated_biome
{
    public const float CLIFF_HEIGHT = 10f;
    public const float CLIFF_PERIOD = 32f;

    public const float HILL_HEIGHT = 52f;
    public const float HILL_PERIOD = 64f;

    protected override void generate_grid()
    {
        // Generate the altitudes
        float xrand = Random.Range(0, 1f);
        float zrand = Random.Range(0, 1f);
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
                    terrain_color = Color.Lerp(colors.grass, colors.dirt, dirt_amt)
                };
            }
    }
}