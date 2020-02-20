using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class biome
{
    public const int CHUNKS_PER_BIOME = world.CHUNK_GRID_SIZE;
    public const int BIOME_TERRAIN_RES = chunk.SIZE * CHUNKS_PER_BIOME;
    float BIOME_AREA = (float)(BIOME_TERRAIN_RES) * (float)(BIOME_TERRAIN_RES);
    float[,] altitude = new float[BIOME_TERRAIN_RES, BIOME_TERRAIN_RES];

    // Get the altitude of this biome at the given 
    // chunkx, chunkz, x, z coordinates (periodic in BIOME_TERRAIN_RES)
    public float get_altitude(int chunkx, int chunkz, int x, int z)
    {
        int globalx = (chunkx + 2) * chunk.SIZE + x;
        int globalz = (chunkz + 2) * chunk.SIZE + z;
        if (globalx < 0) return 0;
        if (globalz < 0) return 0;
        int bx = globalx % BIOME_TERRAIN_RES;
        int bz = globalz % BIOME_TERRAIN_RES;
        return altitude[bx, bz];
    }

    public biome()
    {
        generate_altitudes();
    }

    protected virtual void generate_altitudes() { }

    public class mountain_forest : biome
    {
        const float MOUNTAIN_MIN_SIZE = 300f;
        const float MOUNTAIN_MAX_SIZE = 600f;
        const float MOUNTAIN_AV_SIZE = (MOUNTAIN_MIN_SIZE + MOUNTAIN_MAX_SIZE) / 2f;
        const float PERLIN_PERIOD = 30f;
        const float PERLIN_SCALE = 30f;

        protected override void generate_altitudes()
        {
            float mountain_density = 4.0f / (MOUNTAIN_AV_SIZE * MOUNTAIN_AV_SIZE);
            int mountain_count = (int)(BIOME_AREA * mountain_density);

            // Start with some perlin noise
            if (false)
                for (int i = 0; i < BIOME_TERRAIN_RES; ++i)
                    for (int j = 0; j < BIOME_TERRAIN_RES; ++j)
                        altitude[i, j] =
                            PERLIN_SCALE * Mathf.PerlinNoise(
                            (float)i / PERLIN_PERIOD,
                            (float)j / PERLIN_PERIOD);

            //mountain_count = 1;

            // Add a bunch of pyramids => mountains
            for (int n = 0; n < mountain_count; ++n)
            {
                int x = Random.Range(0, BIOME_TERRAIN_RES);
                int z = Random.Range(0, BIOME_TERRAIN_RES);
                int width = Random.Range(
                    (int)MOUNTAIN_MIN_SIZE,
                    (int)MOUNTAIN_MAX_SIZE);
                float scale = width;
                float rot = Random.Range(0, Mathf.PI * 2);
                procmath.add_pyramid(ref altitude, x, z, width, scale, rot);
            }

            // Normalise to MAX_ALTITUDE
            procmath.rescale(ref altitude, 0, world.MAX_ALTITUDE);
        }
    }
}

public static class world_generator
{
    public static string save_directory()
    {
        return "/home/mick/programming/unity/dont_get_lost/worlds/";
    }

    static biome biome = new biome.mountain_forest();

    // The world-generator representation of a chunk
    public class chunk_info
    {
        public int x;
        public int z;
        public float[,] altitude;

        public string filename(string world_name)
        {
            return save_directory()
                + world_name + "/chunk_" + x + "_" + z;
        }

        public chunk_info(string world, int x, int z)
        {
            this.x = x;
            this.z = z;

            if (!System.IO.File.Exists(filename(world)))
            {
                // File doesn't exist, generate default
                altitude = new float[chunk.TERRAIN_RES, chunk.TERRAIN_RES];
                for (int i = 0; i < chunk.TERRAIN_RES; ++i)
                    for (int j = 0; j < chunk.TERRAIN_RES; ++j)
                        altitude[i, j] = biome.get_altitude(x, z, i, j);

                return;
            }
        }

        public void save(string world_name)
        {
            using (var file = new System.IO.StreamWriter(filename(world_name)))
            {
                file.WriteLine("Hello!");
            }
        }
    }

    public static void generate(string name)
    {
        System.IO.Directory.CreateDirectory(save_directory());
        System.IO.Directory.CreateDirectory(save_directory() + name);
    }

    public static void save_texture_as_png(Texture2D texture, string path)
    {
        byte[] bytes = texture.EncodeToPNG();
        System.IO.File.WriteAllBytes(path, bytes);
    }
}