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

    // Maps x to a linear curve, smoothed towards x = 0 and x = 1
    public static float end_smoothed_linear(float x, float smooth_range)
    {
        if (x < 0) return 0;
        if (x > 1) return 1.0f;

        float d = smooth_range;
        float scale = 1f / (1 - d);

        if (x < d) return scale * x * x / (2 * d);
        if (x < 1 - d) return scale * (x - d / 2f);
        return scale * (1f - d - (1f - x) * (1f - x) / (2f * d));
    }

    // Tools for manipulating 2D arrays of floats
    public static class float_2D_tools
    {
        // Generate a random normally-distributed number 
        // with mean 0, std dev = 1, using a box-muller tranform
        public static float random_normal()
        {
            float u1 = Random.Range(0, 1f);
            float u2 = Random.Range(0, 1f);
            return Mathf.Sqrt(-2 * Mathf.Log(u1)) * Mathf.Cos(2 * Mathf.PI * u2);
        }

        // Check if x, z is in range for a 2d array
        public static bool in_range(ref float[,] arr, int x, int z)
        {
            return x >= 0 && x < arr.GetLength(0) &&
                   z >= 0 && z < arr.GetLength(1);
        }

        // Rescale a float array so it has the given minimum and maximum values
        public static void rescale(ref float[,] arr,
            float min, float max, remap remap)
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
                {
                    float a = (arr[i, j] - min_found)
                        / (max_found - min_found);
                    arr[i, j] = min + remap(a) * (max - min);
                }
        }

        // Smooth an array by a given amount
        public static void smooth(ref float[,] arr, float amt)
        {
            var tmp = new float[arr.GetLength(0), arr.GetLength(1)];

            for (int i = 1; i < arr.GetLength(0) - 1; ++i)
                for (int j = 1; j < arr.GetLength(1) - 1; ++j)
                {
                    tmp[i, j] = 0.25f * (
                        arr[i + 1, j] + arr[i - 1, j] +
                        arr[i, j + 1] + arr[i, j - 1]);
                }

            for (int i = 1; i < arr.GetLength(0) - 1; ++i)
                for (int j = 1; j < arr.GetLength(1) - 1; ++j)
                    arr[i, j] = amt * tmp[i, j] + (1 - amt) * arr[i, j];
        }

        // Overload of the above, with remap = identity
        public static void rescale(ref float[,] arr,
            float min, float max)
        {
            rescale(ref arr, min, max, (f) => f);
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

        // Add a smoothed pyramid to the given array
        public static void add_smoothed_pyramid(ref float[,] arr,
            int x, int z, int width, float height, remap height_map,
            float smoothing_amt, float rotation = 0)
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

                    float i_rot = Mathf.Abs(cos * i_frac - sin * j_frac);
                    float j_rot = Mathf.Abs(sin * i_frac + cos * j_frac);
                    i_rot = end_smoothed_linear(i_rot, smoothing_amt);
                    j_rot = end_smoothed_linear(j_rot, smoothing_amt);

                    float h = 1.0f - i_rot - j_rot;

                    if (h < 0) h = 0;

                    arr[ai, aj] += height_map(h) * height;
                }
        }

        // Overload of the above with height_map = identity
        public static void add_smoothed_pyramid(ref float[,] arr,
            int x, int z, int width, float height,
            float smoothing_amt, float rotation = 0)
        {
            add_smoothed_pyramid(ref arr, x, z, width, height,
                (f) => f, smoothing_amt, rotation);
        }
    }
}
