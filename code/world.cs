using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The in-game representation of the world
public class world : MonoBehaviour
{
    public const float MAX_ALTITUDE = 256f;
    public const float SEA_LEVEL = 16f;

    // The grid of chunks around the player
    public const int CHUNK_RANGE = 2;
    public const int CHUNK_GRID_SIZE = CHUNK_RANGE * 2 + 1;
    List<chunk> loaded_chunks = new List<chunk>();
    chunk[,] chunk_grid = new chunk[CHUNK_GRID_SIZE, CHUNK_GRID_SIZE];

    // The player
    public player player { get; private set; }

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
        //world_generator.generate("world_1");

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
}