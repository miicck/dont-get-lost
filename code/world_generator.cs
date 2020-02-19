using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class world_generator
{
    public static string save_directory()
    {
        return "/home/mick/programming/unity/dont_get_lost/worlds/";
    }

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
                    {
                        float xf = chunk.SIZE * x + i;
                        float zf = chunk.SIZE * z + j;
                        altitude[i, j] = Mathf.PerlinNoise(xf / 256f, zf / 256f);
                    }

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