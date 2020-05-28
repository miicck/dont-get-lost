using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A class containing mathematical functions 
// that are useful for procedural generation
public static class procmath
{
    // Returns a random number generator, seeded by multiple ints
    public static System.Random multiseed_random(params int[] seeds)
    {
        // Create a series of random number generators, each taking
        // one of the successive input seeds as the seed
        System.Random[] rands = new System.Random[seeds.Length];
        for (int i = 0; i < seeds.Length; ++i)
            rands[i] = new System.Random(seeds[i]);

        // Use the above random number generators to create a 
        // new pseudorandom seed
        int seed = 0;
        for (int i = 0; i < seeds.Length; ++i)
            seed += rands[i].Next() / seeds.Length;

        // Create the final random number generator
        return new System.Random(seed);
    }

    // Extend system.random to generate floating point ranges
    public static float range(this System.Random gen, float min, float max)
    {
        return (float)(gen.NextDouble() * (max - min)) + min;
    }

    // Extend system.random to generate integer ranges
    public static int range(this System.Random gen, int min, int max)
    {
        if (max == min) return min;
        if (max < min) throw new System.Exception("max < min!");
        return min + gen.Next() % (max - min);
    }

    // Maps for a single floating point value
    public static class maps
    {
        /// <summary> Smoothly increases from 0 at x = thresh to 1 at x = 1 </summary>
        public static float smooth_max_cos(float x, float thresh = 0f)
        {
            if (x <= thresh) return 0;
            if (x >= 1) return 1;
            return 1 - Mathf.Cos((x - thresh) * Mathf.PI / (2 * (1.0f - thresh)));
        }

        /// <summary> Maps x to a linear curve, smoothed towards x = 0 and x = 1</summary>
        public static float end_smoothed_linear(float x, float smooth_range)
        {
            if (x <= 0) return 0;
            if (x >= 1) return 1.0f;

            float d = smooth_range;
            float scale = 1f / (1 - d);

            if (x < d) return scale * x * x / (2 * d);
            if (x < 1 - d) return scale * (x - d / 2f);
            return scale * (1f - d - (1f - x) * (1f - x) / (2f * d));
        }

        /// <summary> Map x to something which switches on at start and linearly
        /// increases to 1 at end</summary>
        public static float linear_turn_on(float x, float start, float end)
        {
            if (x < start) return 0;
            if (x > end) return 1;
            return (x - start) / (end - start);
        }

        /// <summary> Map x to a smoothed version of the floor function</summary>
        public static float smoothed_floor(float x, float smoothing)
        {
            float xf = Mathf.Floor(x);
            float diff = x - xf;
            float add = 0;
            if (diff > smoothing)
                add = (diff - smoothing) / (1 - smoothing);
            add = end_smoothed_linear(add, 0.25f);
            return xf + add;
        }

        /// <summary> Asymptotically approach +/- 1 as x -> +/- infinity </summary>
        public static float asmptote_to_1(float x)
        {
            if (x < 0) return asmptote_to_1(-x);
            return 1f - Mathf.Exp(-x);
        }
    }

    // Pick a choice from the given array, with a Gaussian probability with the given width
    // centred at a fractional position p along the array.
    public static T sliding_scale<T>(float p, T[] choices, System.Random random, float width = 0.25f)
    {
        // Generate the probabilities (normalization doesn't matter)
        float[] probs = new float[choices.Length];
        float total = 0;
        for (int i = 0; i < probs.Length; ++i)
        {
            float di = probs.Length * p - i;
            probs[i] = Mathf.Exp(-di * di / (2 * width * width));
            total += probs[i];
        }

        // Choose with those probabilities
        float rand = random.range(0f, total);
        total = 0;
        for (int i = 0; i < probs.Length; ++i)
        {
            total += probs[i];
            if (total > rand)
                return choices[i];
        }

        throw new System.Exception("Shouldn't be able to get here!");
    }

    // Tools for manipulating 2D arrays of floats
    public static class float_2D_tools
    {
        // Remap a float to another float
        public delegate float remap(float f);

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

                    float i_frac = i / (float)(width / 2);
                    float j_frac = j / (float)(width / 2);

                    float i_rot = Mathf.Abs(cos * i_frac - sin * j_frac);
                    float j_rot = Mathf.Abs(sin * i_frac + cos * j_frac);
                    i_rot = maps.end_smoothed_linear(i_rot, smoothing_amt);
                    j_rot = maps.end_smoothed_linear(j_rot, smoothing_amt);

                    float h = 1.0f - i_rot - j_rot;

                    if (h < 0) h = 0;

                    arr[ai, aj] += height_map(h) * height;
                }
        }

        // Add a sand-dune like object
        public static void add_sand_dune(ref float[,] arr,
            int x, int z, int footprint, float height)
        {
            for (int i = -footprint / 2; i <= footprint / 2; ++i)
                for (int j = -footprint / 2; j <= footprint / 2; ++j)
                {
                    int ai = x + i;
                    int aj = z + j;
                    if (!in_range(ref arr, ai, aj)) continue;

                    float i_frac = i / (float)(footprint / 4);
                    if (i_frac < -1f) continue;
                    if (i_frac > 1f) continue;
                    float j_frac = (1f + j / (float)(footprint / 2)) / 2f;

                    float h = (1f + Mathf.Cos(i_frac * Mathf.PI)) / 2f;
                    float cusp = 0.01f + 0.1f * h;

                    float amp = 0f;
                    if (j_frac < cusp) amp = maps.end_smoothed_linear(j_frac / cusp, 0.1f);
                    else amp = maps.end_smoothed_linear((1 - j_frac) / (1 - cusp), 0.1f);
                    h *= amp * amp;

                    arr[ai, aj] += h * height;
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

        // Apply the given apply_func to the array, where
        // f is the floating point value and g is the value
        // of a guassian centred at x, z with the given width.
        public delegate float apply_func(float f, float g);
        public static void apply_guassian(ref float[,] arr,
            int x, int z, int width, apply_func func)
        {
            for (int i = 0; i < arr.GetLength(0); ++i)
                for (int j = 0; j < arr.GetLength(1); ++j)
                {
                    float r2 = (i - x) * (i - x) + (j - z) * (j - z);
                    r2 /= (float)(width * width);
                    arr[i, j] = func(arr[i, j], Mathf.Exp(-r2 / 2));
                }
        }

        // Return the gradients of the given array, evaluated using 
        // finite-differences
        public static Vector2[,] get_gradients(float[,] arr)
        {
            var grad = new Vector2[arr.GetLength(0), arr.GetLength(1)];
            for (int i = 1; i < arr.GetLength(0) - 1; ++i)
                for (int j = 1; j < arr.GetLength(1) - 1; ++j)
                {
                    grad[i, j].x = (arr[i + 1, j] - arr[i - 1, j]) / 2f;
                    grad[i, j].y = (arr[i, j + 1] - arr[i, j - 1]) / 2f;
                }
            return grad;
        }
    }
}