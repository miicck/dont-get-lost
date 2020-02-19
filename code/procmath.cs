using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A class containing mathematical functions 
// that are useful for procedural generation
public static class procmath
{
    // Smoothly increases from 0 to 1
    public static float smooth_max_cos(float x, float thresh = 0f)
    {
        if (x < thresh) return 0;
        if (x > 1) return 1;
        return 1 - Mathf.Cos((x - thresh) * Mathf.PI / (2 * (1.0f - thresh)));
    }

    // Add a Guassian with width w amplitude a to the given array 
    // centred at x, z (w,x,z are in fractional coordinates)
    public static void add_guassian(ref float[,] arr, float x, float z, float a, float w)
    {
        int width = arr.GetLength(0);
        int height = arr.GetLength(1);
        for (int i = 0; i < width; ++i)
            for (int j = 0; j < height; ++j)
            {
                float fi = ((float)i / (float)width) - x;
                float fj = ((float)j / (float)height) - z;

                arr[i, j] += Mathf.Exp(-(fi * fi + fj * fj) / (w * w));
            }
    }

    // Returns a pyramid centred at x = z = 0.5.
    public static float pyramid(float x, float z)
    {
        x -= Mathf.Floor(x);
        z -= Mathf.Floor(z);
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
