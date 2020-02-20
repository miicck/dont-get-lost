using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A class containing mathematical functions 
// that are useful for procedural generation
public static class procmath
{
    // Remap a float to another float
    public delegate float remap(float f);

    // Smoothly increases from 0 at x = thresh to 1 at x = 1
    public static float smooth_max_cos(float x, float thresh = 0f)
    {
        if (x < thresh) return 0;
        if (x > 1) return 1;
        return 1 - Mathf.Cos((x - thresh) * Mathf.PI / (2 * (1.0f - thresh)));
    }

    // Check if x, z is in range for a 2d array
    public static bool in_range(ref float[,] arr, int x, int z)
    {
        return x >= 0 && x < arr.GetLength(0) &&
               z >= 0 && z < arr.GetLength(1);
    }

    // Rescale a float array so it has the given minimum and maximum values
    public static void rescale(ref float[,] arr, float min, float max)
    {
        float min_found = float.MaxValue;
        float max_found = float.MinValue;
        for (int i = 0; i < arr.GetLength(0); ++i)
            for (int j = 0; j < arr.GetLength(1); ++j)
            {
                float a = arr[i, j];
                if (a > max_found) max_found = a;
                if (a < min_found) min_found = a;
            }

        for (int i = 0; i < arr.GetLength(0); ++i)
            for (int j = 0; j < arr.GetLength(1); ++j)
                arr[i, j] = min +
                (max - min) * (arr[i, j] - min_found)
                / (max_found - min_found);
    }

    // Adds a smooth, dome-like structure to the given array at the
    // given coordinates
    public static void add_smooth_dome(ref float[,] arr,
        int x, int z, int width, float height, remap height_map)
    {
        for (int i = -width / 2; i <= width / 2; ++i)
            for (int j = -width / 2; j <= width / 2; ++j)
            {
                int ai = x + i;
                int aj = z + j;
                if (!in_range(ref arr, ai, aj)) continue;

                float i_frac = (float)i / (float)(width / 2);
                float j_frac = (float)j / (float)(width / 2);
                float r = Mathf.Sqrt(i_frac * i_frac + j_frac * j_frac);
                if (r > 1) continue;

                float h = (1 + Mathf.Cos(r * Mathf.PI)) / 2f;
                if (h < 0) h = 0;
                arr[ai, aj] += height_map(h) * height;
            }
    }

    // Overload of the above with height_map = identity
    public static void add_smooth_dome(ref float[,] arr,
    int x, int z, int width, float height)
    {
        add_smooth_dome(ref arr, x, z, width, height, (f) => f);
    }

    // Add a bulbus cone to the given array
    public static void add_bulbus_cone(ref float[,] arr,
        int x, int z, int max_radius, float height, float bulb_freq,
        remap height_map, float rotation = 0)
    {
        for (int i = -max_radius; i <= max_radius; ++i)
            for (int j = -max_radius; j <= max_radius; ++j)
            {
                int ai = x + i;
                int aj = z + j;
                if (!in_range(ref arr, ai, aj)) continue;

                float r = Mathf.Sqrt((float)(i * i + j * j)) / (float)max_radius;
                float theta = Mathf.Atan((float)j / (float)i);
                if (i == 0) theta = Mathf.PI / 2;
                float rmod = (Mathf.Cos(theta * bulb_freq) + 2f);
                r *= rmod;

                if (r > 1) continue;

                float h = (1 + Mathf.Cos(r * Mathf.PI)) / 2f;

                if (h < 0) continue;
                if (h > 1) h = 1;

                arr[ai, aj] += height_map(h) * height;
            }
    }

    // Adds a pyramid-like structure to the given array
    // at the given coordinates
    public static void add_pyramid(ref float[,] arr,
        int x, int z, int width, float height, remap height_map,
        float rotation = 0)
    {
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);

        for (int i = -width / 2; i <= width / 2; ++i)
            for (int j = -width / 2; j <= width / 2; ++j)
            {
                int ai = x + i;
                int aj = z + j;
                if (!in_range(ref arr, ai, aj)) continue;

                float i_frac = (float)i / (float)(width / 2);
                float j_frac = (float)j / (float)(width / 2);

                float i_rot = cos * i_frac - sin * j_frac;
                float j_rot = sin * i_frac + cos * j_frac;

                float h = 1.0f - Mathf.Abs(i_rot) - Mathf.Abs(j_rot);
                if (h < 0) h = 0;

                arr[ai, aj] += height_map(h) * height;
            }
    }

    // Overload of the above with height_map = identity
    public static void add_pyramid(ref float[,] arr,
        int x, int z, int width, float height,
        float rotation = 0)
    {
        add_pyramid(ref arr, x, z, width, height, (f) => f, rotation);
    }

    // Returns a pyramid centred at x = z = 0.5.
    public static float pyramid(float x, float z, bool periodic = true)
    {
        if (periodic)
        {
            x -= Mathf.Floor(x);
            z -= Mathf.Floor(z);
        }
        else
        {
            if (x > 1) return 0;
            if (x < 0) return 0;
            if (z > 1) return 0;
            if (z < 0) return 0;
        }
        return 1 - (Mathf.Abs(x - 0.5f) + Mathf.Abs(z - 0.5f)) * 2;
    }

    // Load a texture from Resources/noise_maps/
    static Dictionary<string, Texture2D> noise_maps = new Dictionary<string, Texture2D>();
    public static Color noise_map(string name, float x, float z)
    {
        Texture2D map;
        if (!noise_maps.TryGetValue(name, out map))
        {
            map = Resources.Load<Texture2D>("noise_maps/" + name);
            noise_maps[name] = map;
        }

        return map.GetPixelBilinear(x, z);
    }

    // Attempt to model the flow of rain shaping a terrain
    // using a biased random walk.
    static float[,] rain_grid = null;
    public static float rain_map(float x, float z)
    {
        if (rain_grid == null)
        {
            // Initialize the grid to 0
            rain_grid = new float[1024, 1024];
            for (int i = 0; i < 1024; ++i)
                for (int j = 0; j < 1024; ++j)
                    rain_grid[i, j] = 0;

            // The move directions
            int[] dxs = { 0, 0, -1, 1 };
            int[] dzs = { -1, 1, 0, 0 };

            // Run each droplet
            for (int droplet = 0; droplet < 10; ++droplet)
            {
                int i = Random.Range(0, 1024);
                int j = Random.Range(0, 1024);
                int count = 0;

                while (i >= 1 && j >= 1 && i < 1024 - 1 && j < 1024 - 1 && count < 100)
                {
                    ++count;
                    float[] probs = new float[4];
                    float total_prob = 0;

                    for (int n = 0; n < 4; ++n)
                    {
                        probs[n] = rain_grid[i + dxs[n], j + dzs[n]] + 1000.0f;
                        total_prob += probs[n];
                    }

                    float sel = Random.Range(0, total_prob);
                    total_prob = 0;
                    int n_sel = 0;
                    for (int n = 0; n < 4; ++n)
                    {
                        total_prob += probs[n];
                        if (total_prob > sel)
                        {
                            n_sel = n;
                            break;
                        }
                    }

                    i += dxs[n_sel];
                    j += dzs[n_sel];

                    rain_grid[i, j] += 0.1f;
                }
            }

            float max = float.MinValue;
            float min = float.MaxValue;
            for (int i = 0; i < 1024; ++i)
                for (int j = 0; j < 1024; ++j)
                {
                    float r = rain_grid[i, j];
                    if (r > max) max = r;
                    if (r < min) min = r;
                }

            for (int i = 0; i < 1024; ++i)
                for (int j = 0; j < 1024; ++j)
                    rain_grid[i, j] = (rain_grid[i, j] - min) / (max - min);

        }

        x *= rain_grid.GetLength(0);
        z *= rain_grid.GetLength(1);

        int xi = (int)x;
        int zi = (int)z;
        float xr = x - (float)xi;
        float zr = z - (float)zi;

        if (xi < 0) xi = 0;
        if (zi < 0) zi = 0;
        xi = xi % (rain_grid.GetLength(0) - 1);
        zi = zi % (rain_grid.GetLength(1) - 1);

        float f00 = rain_grid[xi, zi];
        float f01 = rain_grid[xi, zi + 1];
        float f10 = rain_grid[xi + 1, zi];
        float f11 = rain_grid[xi + 1, zi + 1];

        return (f00 * (1 - xr) + f10 * xr) * (1 - zr) +
               (f01 * (1 - xr) + f11 * xr) * zr;
    }
}
