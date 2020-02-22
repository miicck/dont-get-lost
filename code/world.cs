using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The in-game representation of the world
public class world : MonoBehaviour
{
    public const float MAX_ALTITUDE = 256f;
    public const float SEA_LEVEL = 16f;
    public const float RENDER_RANGE = chunk.SIZE * 3;

    // The loaded chunks around the player
    List<chunk> loaded_chunks = new List<chunk>();

    // Find a particular x, z chunk if it exists
    chunk find_in_loaded(int x, int z)
    {
        foreach (var c in loaded_chunks)
            if (c.check_coords(x, z))
                return c;
        return null;
    }

    // The player
    public player player { get; private set; }

    public int player_chunk_x
    {
        get
        {
            return (int)Mathf.Round(
                player.transform.position.x / chunk.SIZE);
        }
    }

    public int player_chunk_z
    {
        get
        {
            return (int)Mathf.Round(
                player.transform.position.z / chunk.SIZE);
        }
    }

    // Check if a chunk at x, z is in render range
    bool chunk_in_range(int x, int z)
    {
        var corner_dxs = new float[] { -0.5f, -0.5f, 0.5f, 0.5f };
        var corner_dzs = new float[] { -0.5f, 0.5f, 0.5f, -0.5f };

        // Find the nearest corner to the player
        float min_dis = float.MaxValue;
        for (int n = 0; n < 4; ++n)
        {
            float xc = ((float)x + corner_dxs[n]) * chunk.SIZE;
            float zc = ((float)z + corner_dzs[n]) * chunk.SIZE;

            float dx = xc - player.transform.position.x;
            float dz = zc - player.transform.position.z;

            float dis = dx * dx + dz * dz;
            if (dis < min_dis)
                min_dis = dis;
        }

        // Check if that corner is within render range
        return Mathf.Sqrt(min_dis) <= RENDER_RANGE;
    }

    void update_chunks()
    {
        // Get rid of chunks that are too far away
        foreach (var c in loaded_chunks.ToArray())
            if (!chunk_in_range(c.x, c.z))
            {
                loaded_chunks.Remove(c);
                Destroy(c.gameObject);
            }

        // Check that all the neccasary chunks
        // exist, if not, generate them
        int chunk_range = (int)((RENDER_RANGE / chunk.SIZE) + 1);
        for (int dx = -chunk_range; dx <= chunk_range; ++dx)
            for (int dz = -chunk_range; dz <= chunk_range; ++dz)
            {
                // The actual world chunk coordinates
                int xc = player_chunk_x + dx;
                int zc = player_chunk_z + dz;
                if (!chunk_in_range(xc, zc))
                    continue;

                // See if this chunk already exists
                foreach (var c in loaded_chunks)
                    if (c.check_coords(xc, zc))
                        goto found;

                // Create a new chunk, add it to the
                // loaded chunks list, update the chunk grid.
                var new_chunk = chunk.generate(xc, zc);
                new_chunk.transform.SetParent(transform);
                loaded_chunks.Add(new_chunk);

            found: continue;
            }

        // Update chunk neighbours
        foreach (var c in loaded_chunks)
            c.update_neighbours(
                find_in_loaded(c.x, c.z + 1),
                find_in_loaded(c.x + 1, c.z),
                find_in_loaded(c.x, c.z - 1),
                find_in_loaded(c.x - 1, c.z)
            );
    }

    void Start()
    {
        // Create the player
        player = player.create();

        // Create the sun
        var sun = new GameObject("sun").AddComponent<Light>();
        sun.transform.position = Vector3.zero;
        sun.transform.LookAt(new Vector3(1, -1, 1));
        sun.type = LightType.Directional;

        // Remove the skybox and ambient lighting
        RenderSettings.skybox = null;
        RenderSettings.ambientSkyColor = Color.black;
    }

    void Update()
    {
        // Make sure the chunks are loaded properly
        // around the player
        update_chunks();

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
}