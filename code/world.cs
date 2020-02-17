using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class world : MonoBehaviour
{
    // MAX_ALTITUDE also sets the general scale
    // of terrain features
    public const float MAX_ALTITUDE = 256f;
    public const float MOUNTAIN_SIZE = MAX_ALTITUDE * 0.75f;
    public const float MOUNTAIN_PERIOD = 1.123f * MOUNTAIN_SIZE;
    public const float HILL_SIZE = MAX_ALTITUDE * 0.24f;
    public const float HILL_PERIOD = 1.254f * HILL_SIZE;
    public const float TERRAIN_ROUGHNESS_SIZE = player.HEIGHT / 10f;
    public const float TERRAIN_ROUGHNESS_PERIOD = 1.544f * TERRAIN_ROUGHNESS_SIZE;

    // The grid of chunks around the player
    public const int CHUNK_RANGE = 1;
    public const int CHUNK_GRID_SIZE = CHUNK_RANGE * 2 + 1;
    List<chunk> loaded_chunks = new List<chunk>();
    chunk[,] chunk_grid = new chunk[CHUNK_GRID_SIZE, CHUNK_GRID_SIZE];

    // The player
    public player player { get; private set; }

    void OnDrawGizmos()
    {
        for (int x = 0; x < CHUNK_GRID_SIZE; ++x)
            for (int z = 0; z < CHUNK_GRID_SIZE; ++z)
            {
                if (chunk_grid[x, z] == null) continue;
                var p = chunk_grid[x, z].transform.position;
                Gizmos.DrawWireCube(
                    p + new Vector3(chunk.SIZE / 2, MAX_ALTITUDE / 2, chunk.SIZE / 2),
                    new Vector3(chunk.SIZE, MAX_ALTITUDE, chunk.SIZE));
            }
    }

    void update_chunks(Vector3 player_position)
    {
        // Find which chunk the player is in
        int player_x = (int)(player_position.x / chunk.SIZE - 0.5f);
        int player_z = (int)(player_position.z / chunk.SIZE - 0.5f);

        // Get rid of chunks that are too far away
        foreach (var c in loaded_chunks.ToArray())
            if (!c.in_range(player_x, player_z))
            {
                loaded_chunks.Remove(c);
                Destroy(c.gameObject);
            }

        // Check that all the neccasary chunks
        // exist, if not, generate them
        for (int x = 0; x < CHUNK_GRID_SIZE; ++x)
            for (int z = 0; z < CHUNK_GRID_SIZE; ++z)
            {
                // The actual world chunk coordinates
                int xc = player_x - CHUNK_RANGE + x;
                int zc = player_z - CHUNK_RANGE + z;

                // See if this chunk already exists
                chunk found = null;
                foreach (var c in loaded_chunks)
                    if (c.check_coords(xc, zc))
                    {
                        found = c;
                        break;
                    }

                if (found != null)
                {
                    // Chunk found, update the chunk grid
                    chunk_grid[x, z] = found;
                    continue;
                }

                // Create a new chunk, add it to the
                // loaded chunks list, update the chunk grid.
                var new_chunk = chunk.generate(xc, zc);
                new_chunk.transform.SetParent(transform);
                loaded_chunks.Add(new_chunk);
                chunk_grid[x, z] = new_chunk;
            }

        // Update the neighbours of each chunk
        for (int x = 0; x < CHUNK_GRID_SIZE; ++x)
            for (int z = 0; z < CHUNK_GRID_SIZE; ++z)
            {
                chunk north = null;
                chunk east = null;
                chunk south = null;
                chunk west = null;

                if (x > 0) east = chunk_grid[x - 1, z];
                if (z > 0) south = chunk_grid[x, z - 1];
                if (x < CHUNK_GRID_SIZE - 1) west = chunk_grid[x + 1, z];
                if (z < CHUNK_GRID_SIZE - 1) north = chunk_grid[x, z + 1];

                chunk_grid[x, z].update_neighbours(north, east, south, west);
            }
    }

    void Start()
    {
        player = player.create();

        var sun = new GameObject("sun").AddComponent<Light>();
        sun.transform.position = Vector3.zero;
        sun.transform.LookAt(new Vector3(1, -1, 1));
        sun.type = LightType.Directional;

        RenderSettings.skybox = null;
        RenderSettings.ambientSkyColor = Color.black;
    }

    void Update()
    {
        // Make sure the chunks are loaded properly
        // around the player
        update_chunks(player.transform.position);

        // Toggle cursor visibility
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    // How mountainous a particular point is on a scale of [0,1]
    public static float mountain_amt(float x, float z)
    {
        float m = Mathf.PerlinNoise(
            4.5647f + x / MOUNTAIN_PERIOD,
            -10.54f + z / MOUNTAIN_PERIOD);
        if (m < 0.5f) return 0;
        return 2 * (m - 0.5f);
    }

    // How hilly a particular point is on a scale of [0,1]
    public static float hill_amt(float x, float z)
    {
        float h = Mathf.PerlinNoise(
            7.4343f + x / HILL_PERIOD,
            -3.334f + z / HILL_PERIOD);
        if (h < 0.5f) return 0;
        return 2 * (h - 0.5f);
    }

    // How rough the terrain is at a particular point on a scale of [0,1]
    public static float terrain_roughness(float x, float z)
    {
        return Mathf.PerlinNoise(
            6.432f + x / TERRAIN_ROUGHNESS_PERIOD,
            3.322f + z / TERRAIN_ROUGHNESS_PERIOD
        );
    }

    public static float altitude(float x, float z)
    {
        float m = mountain_amt(x, z);
        float h = hill_amt(x, z);
        float r = terrain_roughness(x, z);

        m *= Mathf.PerlinNoise(x / MOUNTAIN_SIZE, z / MOUNTAIN_SIZE);
        h *= Mathf.PerlinNoise(x / HILL_SIZE, z / HILL_SIZE);
        r *= Mathf.PerlinNoise(x / TERRAIN_ROUGHNESS_SIZE, z / TERRAIN_ROUGHNESS_SIZE);

        float a = m * MOUNTAIN_SIZE + h * HILL_SIZE + r * TERRAIN_ROUGHNESS_SIZE;

        return Mathf.Min(a, MAX_ALTITUDE);
    }
}