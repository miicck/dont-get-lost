using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The in-game world
public static class world
{
    // The name this world is (to be) saved as
    public static string name = "world";

    // The folder to save the world in
    public static string save_folder()
    {
        // First, ensure the folder exists 
        string folder = "/home/mick/programming/unity/dont_get_lost/worlds/" + name;
        System.IO.Directory.CreateDirectory(folder);
        return folder;
    }

    // Destroy the world!
    public static void destroy()
    {
        foreach (var c in chunk_grid)
            if (c != null)
                c.destroy();

        foreach (var b in biome_grid)
            if (b != null)
                b.destroy();
    }

    // World-scale geograpical constants
    public const int SEA_LEVEL = 16;
    public const float MAX_ALTITUDE = 128f;

    // The grid of chunks
    static chunk[,] chunk_grid = new chunk[0, 0];
    static int chunk_grid_x_centre = 0;
    static int chunk_grid_z_centre = 0;

    // Check if i,j are in range for the chunk_grid array
    static bool in_chunk_grid_range(int i, int j)
    {
        return i >= 0 && j >= 0 &&
               i < chunk_grid.GetLength(0) &&
               j < chunk_grid.GetLength(1);
    }

    // The grid of biomes
    static biome[,] biome_grid = new biome[0, 0];
    static int biome_grid_x_centre = 0;
    static int biome_grid_z_centre = 0;

    static List<chunk> stored_chunks;
    static List<biome> stored_biomes;
    static void store_grids()
    {
        // Store a list of the chunks in the current grid
        // and set the current grid to null
        stored_chunks = new List<chunk>();
        for (int x = 0; x < chunk_grid.GetLength(0); ++x)
            for (int z = 0; z < chunk_grid.GetLength(1); ++z)
            {
                var c = chunk_grid[x, z];
                chunk_grid[x, z] = null;
                if (c != null) stored_chunks.Add(c);
            }

        // Store a list of the chunks in the current grid
        stored_biomes = new List<biome>();
        for (int x = 0; x < biome_grid.GetLength(0); ++x)
            for (int z = 0; z < biome_grid.GetLength(1); ++z)
            {
                var b = biome_grid[x, z];
                biome_grid[x, z] = null;
                if (b != null) stored_biomes.Add(b);
            }
    }

    static void restore_grids()
    {
        // Restore the list of stored chunks to the current grid
        // (destroy them if they go out of range due to a grid
        //  resize)
        foreach (var c in stored_chunks)
        {
            int x = chunk_x_to_grid(c.x);
            int z = chunk_z_to_grid(c.z);

            if (utils.in_range(x, chunk_grid.GetLength(0)) &&
                utils.in_range(z, chunk_grid.GetLength(1)))
                chunk_grid[x, z] = c;
            else
                c.destroy();
        }
        stored_chunks = null;

        // Restore the list of stored biomes to the current grid
        // (destroy them if they go out of range due to a grid
        //  resize)
        foreach (var b in stored_biomes)
        {
            int x = biome_x_to_grid(b.x);
            int z = biome_z_to_grid(b.z);

            if (utils.in_range(x, biome_grid.GetLength(0)) &&
                utils.in_range(z, biome_grid.GetLength(1)))
                biome_grid[x, z] = b;
            else
                b.destroy();

        }
        stored_biomes = null;
    }

    static void update_grid_sizes()
    {
        // Setup the new chunk grid size
        int chunks = 1 + 2 * (int)Mathf.Ceil(game.render_range / chunk.SIZE);

        // Setup the new biome grid size
        float max_chunk_range = chunk.SIZE * chunks / 2f;
        int biomes = 3 + 2 * (int)Mathf.Ceil(max_chunk_range / biome.SIZE);

        if (chunk_grid == null ||
            biome_grid == null ||
            chunks != chunk_grid.GetLength(0) ||
            biomes != biome_grid.GetLength(0))
        {
            store_grids();
            chunk_grid = new chunk[chunks, chunks];
            biome_grid = new biome[biomes, biomes];
            restore_grids();
        }
    }

    static void update_grid_centre(Vector3 centre)
    {
        store_grids();

        // Set the centre positions of the chunk/biome grids
        chunk_grid_x_centre = world_to_chunk(centre.x);
        chunk_grid_z_centre = world_to_chunk(centre.z);

        biome_grid_x_centre = world_to_biome(centre.x);
        biome_grid_z_centre = world_to_biome(centre.z);

        restore_grids();
    }

    public static bool chunk_in_range(int x, int z, Vector3 centre)
    {
        var corner_dxs = new float[] { -0.5f, -0.5f, 0.5f, 0.5f };
        var corner_dzs = new float[] { -0.5f, 0.5f, 0.5f, -0.5f };

        // Find the nearest corner to the centre
        float min_dis = float.MaxValue;
        for (int n = 0; n < 4; ++n)
        {
            float xc = ((float)x + corner_dxs[n]) * chunk.SIZE;
            float zc = ((float)z + corner_dzs[n]) * chunk.SIZE;

            float dx = xc - centre.x;
            float dz = zc - centre.z;

            float dis = dx * dx + dz * dz;
            if (dis < min_dis)
                min_dis = dis;
        }

        // Check if that corner is within render range
        return Mathf.Sqrt(min_dis) <= game.render_range;
    }

    public static void update_grid(Vector3 centre)
    {
        update_grid_sizes();
        update_grid_centre(centre);

        bool[,] biome_needed = new bool[biome_grid.GetLength(0),
                                        biome_grid.GetLength(1)];

        // Get rid of chunks that are too far away and ensure
        // chunks that need to be loaded are loaded
        // also flag which biomes are needed.
        int[] dxs = { -1, -1, -1, 0, 1, 1, 1, 0 };
        int[] dzs = { -1, 0, 1, 1, 1, 0, -1, -1 };
        var corner_dxs = new float[] { -0.5f, -0.5f, 0.5f, 0.5f };
        var corner_dzs = new float[] { -0.5f, 0.5f, 0.5f, -0.5f };

        for (int x = 0; x < chunk_grid.GetLength(0); ++x)
            for (int z = 0; z < chunk_grid.GetLength(1); ++z)
            {
                int xc = grid_to_chunk_x(x);
                int zc = grid_to_chunk_z(z);

                // Work out which biomes this chunk needs to see
                // (flag every biome which contains a chunk corner)
                if (chunk_grid[x, z] != null)
                    for (int n = 0; n < 4; ++n)
                    {
                        float corner_x = chunk_to_world_centre(xc) +
                            corner_dxs[n] * chunk.SIZE;
                        float corner_z = chunk_to_world_centre(zc) +
                            corner_dzs[n] * chunk.SIZE;

                        int bx = biome_x_to_grid(world_to_biome(corner_x));
                        int bz = biome_z_to_grid(world_to_biome(corner_z));
                        biome_needed[bx, bz] = true;

                        // Any biome which is adjecent to a biome containing
                        // a chunk is also needed so that they can be mixed
                        // together to generate said chunk
                        for (int m = 0; m < 8; ++m)
                            biome_needed[bx + dxs[m], bz + dzs[m]] = true;
                    }

                if (chunk_in_range(xc, zc, centre))
                {
                    // Create required chunks
                    if (chunk_grid[x, z] == null)
                        chunk_grid[x, z] = new chunk(xc, zc);
                }
                else if (chunk_grid[x, z] != null)
                {
                    // Destroy unneccasary chunks
                    chunk_grid[x, z].destroy();
                    chunk_grid[x, z] = null;
                }

                // Set chunk neighbours
                if (chunk_grid[x, z] != null)
                    chunk_grid[x, z].update_neighbours(
                        in_chunk_grid_range(x, z + 1) ? chunk_grid[x, z + 1] : null,
                        in_chunk_grid_range(x + 1, z) ? chunk_grid[x + 1, z] : null,
                        in_chunk_grid_range(x, z - 1) ? chunk_grid[x, z - 1] : null,
                        in_chunk_grid_range(x - 1, z) ? chunk_grid[x - 1, z] : null
                    );
            }

        // Get rid of biomes that are too far away
        for (int x = 0; x < biome_grid.GetLength(0); ++x)
            for (int z = 0; z < biome_grid.GetLength(1); ++z)
                if (!biome_needed[x, z])
                {
                    // Destroy unneccasary biomes
                    if (biome_grid[x, z] == null) continue;
                    biome_grid[x, z].destroy();
                    biome_grid[x, z] = null;
                }
    }

    // Work out how much to blend in the biome from the normal
    // direction, given the fracntional normal coord and 
    // the fractional tangential coord witin this biome.
    static float blend_amount(float normal_amt, float tangent_amt)
    {
        float n = normal_amt;
        float t = tangent_amt;

        const float MAX_THRESH = 1 - biome.MIN_BLEND_FRAC;
        const float MIN_THRESH = 1 - biome.MAX_BLEND_FRAC;
        const float DIF_THRESH = MAX_THRESH - MIN_THRESH;

        float thresh = MAX_THRESH - DIF_THRESH * t * t;

        // Linear blend for now
        if (n < thresh) return 0;
        return procmath.smooth_max_cos((n - thresh) / (1 - thresh));
    }

    // Blend biomes together to get the average point
    // information at the given x, z in world coords.
    public static biome.point point(int x, int z)
    {
        // Biome coordinates
        int bx = world_to_biome(x);
        int bz = world_to_biome(z);

        // Coordinates of the biome within the grid
        int gx = biome_x_to_grid(bx);
        int gz = biome_z_to_grid(bz);

        // Fractional coordinate within biome in [-1,1]
        float xf = 2 * (x - biome_to_world_centre(bx)) / biome.SIZE;
        float zf = 2 * (z - biome_to_world_centre(bz)) / biome.SIZE;
        int dx = utils.sign(xf);
        int dz = utils.sign(zf);
        float xamt = blend_amount(Mathf.Abs(xf), Mathf.Abs(zf));
        float zamt = blend_amount(Mathf.Abs(zf), Mathf.Abs(xf));
        float damt = Mathf.Min(xamt, zamt);

        // Blend in the centre biome with weight 1
        if (biome_grid[gx, gz] == null)
            biome_grid[gx, gz] = biome.at(bx, bz);
        var points_to_average = new List<biome.point>() { biome_grid[gx, gz].get_point(x, z) };
        var averaging_weights = new List<float>() { 1.0f };

        // Blend in the nearest east/west biome with weight xamt
        if (xamt > 1e-4f)
        {
            if (biome_grid[gx + dx, gz] == null)
                biome_grid[gx + dx, gz] = biome.at(bx + dx, bz);
            points_to_average.Add(biome_grid[gx + dx, gz].get_point(x, z));
            averaging_weights.Add(xamt);
        }

        // Blend in the nearest north/south biome with weight zamt
        if (zamt > 1e-4)
        {
            if (biome_grid[gx, gz + dz] == null)
                biome_grid[gx, gz + dz] = biome.at(bx, bz + dz);
            points_to_average.Add(biome_grid[gx, gz + dz].get_point(x, z));
            averaging_weights.Add(zamt);
        }

        // Blend in the nearest diagonal biome with weight damt
        if (damt > 1e-4)
        {
            if (biome_grid[gx + dx, gz + dz] == null)
                biome_grid[gx + dx, gz + dz] = biome.at(bx + dx, bz + dz);
            points_to_average.Add(biome_grid[gx + dx, gz + dz].get_point(x, z));
            averaging_weights.Add(damt);
        }

        return biome.point.average(points_to_average, averaging_weights);
    }

    public static void draw_gizmos(Vector3 centre)
    {
        // Draw the render range circle
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(centre, game.render_range);

        // Outline the chunk grid
        if (chunk_grid == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new Vector3(
            chunk_to_world_centre(chunk_grid_x_centre), 0,
            chunk_to_world_centre(chunk_grid_z_centre)),
            new Vector3(
                chunk.SIZE * chunk_grid.GetLength(0), 0.1f,
                chunk.SIZE * chunk_grid.GetLength(1))
        );

        // Outline the chunks
        for (int x = 0; x < chunk_grid.GetLength(0); ++x)
            for (int z = 0; z < chunk_grid.GetLength(0); ++z)
            {
                if (chunk_grid[x, z] == null) continue;
                Vector3 pos = new Vector3(
                    chunk_to_world_centre(grid_to_chunk_x(x)), 0,
                    chunk_to_world_centre(grid_to_chunk_z(z)));
                Vector3 size = new Vector3(chunk.SIZE, 0.1f, chunk.SIZE);
                Gizmos.DrawWireCube(pos, size);
            }

        // Outline the biome grid
        if (biome_grid == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(
            biome_to_world_centre(biome_grid_x_centre), 0,
            biome_to_world_centre(biome_grid_z_centre)),
            new Vector3(
                biome.SIZE * biome_grid.GetLength(0), 0.1f,
                biome.SIZE * biome_grid.GetLength(1))
        );

        // Outline the chunks
        for (int x = 0; x < biome_grid.GetLength(0); ++x)
            for (int z = 0; z < biome_grid.GetLength(1); ++z)
            {
                if (biome_grid[x, z] == null) continue;
                Vector3 pos = new Vector3(
                    biome_to_world_centre(grid_to_biome_x(x)), 0,
                    biome_to_world_centre(grid_to_biome_z(z)));
                Vector3 size = new Vector3(biome.SIZE, 0.1f, biome.SIZE);
                Gizmos.DrawWireCube(pos, size);
            }
    }

    // Coordinate conversions
    static float chunk_to_world_centre(int c) { return c * chunk.SIZE; }
    static float biome_to_world_centre(int b) { return b * biome.SIZE; }
    static int world_to_chunk(float w) { return utils.round(w / chunk.SIZE); }
    static int world_to_biome(float w) { return utils.round(w / biome.SIZE); }
    static int chunk_x_to_grid(int xc) { return xc - chunk_grid_x_centre + (chunk_grid.GetLength(0) - 1) / 2; }
    static int chunk_z_to_grid(int zc) { return zc - chunk_grid_z_centre + (chunk_grid.GetLength(1) - 1) / 2; }
    static int biome_x_to_grid(int xb) { return xb - biome_grid_x_centre + (biome_grid.GetLength(0) - 1) / 2; }
    static int biome_z_to_grid(int zb) { return zb - biome_grid_z_centre + (biome_grid.GetLength(1) - 1) / 2; }
    static int grid_to_chunk_x(int gx) { return gx + chunk_grid_x_centre - (chunk_grid.GetLength(0) - 1) / 2; }
    static int grid_to_chunk_z(int gz) { return gz + chunk_grid_z_centre - (chunk_grid.GetLength(1) - 1) / 2; }
    static int grid_to_biome_x(int gx) { return gx + biome_grid_x_centre - (biome_grid.GetLength(0) - 1) / 2; }
    static int grid_to_biome_z(int gz) { return gz + biome_grid_z_centre - (biome_grid.GetLength(1) - 1) / 2; }
}