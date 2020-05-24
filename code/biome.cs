using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// A biome represents the largest singly-generated area of the map
// and is used to generate quantities that exist on the largest length
// scales, such as the terrain.
public abstract class biome : MonoBehaviour
{
    // Set the blend distance to slightly less than a chunk 
    // so we only need blend the outermost chunks in a biome
    public const int BLEND_DISTANCE = chunk.SIZE - 1;

    // The biome coordinates
    public int x { get; private set; }
    public int z { get; private set; }

    // The random number generator specific to this biome
    public System.Random random { get; private set; }

    // The grid of points defining the biome
    protected point[,] grid = new point[SIZE, SIZE];
    protected abstract void generate_grid();

    //#####################//
    // NEIGHBOURING BIOMES //
    //#####################//

    // My neighbouring biomes, if they don't exist already
    // we will generate them.
    biome[,] _neihbours = new biome[3, 3];
    biome get_neighbour(int dx, int dz, bool generate_if_needed = true)
    {
        if (dx == 0 && dz == 0) return this;
        int i = dx + 1;
        int j = dz + 1;
        if (_neihbours[i, j] == null)
        {
            // This catches the case where the neighbouring
            // biome has been destroyed, but not set to null proper
            _neihbours[i, j] = null;

            // Attempt to find already existing biome, otherwise generate one
            var found = generated_biomes.get(x + dx, z + dz);
            if (found != null) _neihbours[i, j] = found;
            else if (generate_if_needed)
                _neihbours[i, j] = generate(x + dx, z + dz);
        }

        return _neihbours[i, j];
    }

    //##################//
    // COORDINATE TOOLS //
    //##################//

    // Get the biome coords at a given location
    public static int[] coords(Vector3 location)
    {
        return new int[]
        {
            Mathf.FloorToInt(location.x / SIZE),
            Mathf.FloorToInt(location.z / SIZE)
        };
    }

    // Check if the biome at x, z is within render range
    // (essentially testing if the render range circle 
    //  intersects the biome square)
    static bool in_range(int x, int z)
    {
        Vector2 player_xz = new Vector2(
            player.current.transform.position.x,
            player.current.transform.position.z
        );

        Vector2 this_xz = new Vector2(
            SIZE * (x + 0.5f),
            SIZE * (z + 0.5f)
        );

        return utils.circle_intersects_square(player_xz, game.render_range, this_xz, SIZE, SIZE);
    }

    /// <summary> Returns true if the given coordinates within a 
    /// biome are in the blended region near the edge. </summary>
    protected static bool in_blend_region(int x_in_biome, int z_in_biome)
    {
        return x_in_biome <= BLEND_DISTANCE || 
               z_in_biome <= BLEND_DISTANCE ||
               x_in_biome >= SIZE - BLEND_DISTANCE || 
               z_in_biome >= SIZE - BLEND_DISTANCE;
    }

    //#########################//
    // CHUNK GRID MANIPULATION //
    //#########################//

    // The size of a biome in chunks per side
    public const int CHUNKS_PER_SIDE = 4;
    public const int SIZE = CHUNKS_PER_SIDE * chunk.SIZE;

    // The grid of chunks within the biome
    chunk[,] chunk_grid = new chunk[CHUNKS_PER_SIDE, CHUNKS_PER_SIDE];

    // Extend the chunk grid indicies to include neighbouring biomes
    chunk extended_chunk_grid(int i, int j, bool generate_if_needed = true)
    {
        // Convert i, j to i, j coordinates in the (dx, dz)^th neighbour
        int dx = 0;
        if (i >= CHUNKS_PER_SIDE) { dx = 1; i -= CHUNKS_PER_SIDE; }
        else if (i < 0) { dx = -1; i += CHUNKS_PER_SIDE; }

        int dz = 0;
        if (j >= CHUNKS_PER_SIDE) { dz = 1; j -= CHUNKS_PER_SIDE; }
        else if (j < 0) { dz = -1; j += CHUNKS_PER_SIDE; }

        var b = get_neighbour(dx, dz, generate_if_needed);
        if (b == null) return null;
        return b.chunk_grid[i, j];
    }

    // Update the chunk neighbours in this biome, including
    // neighbours from neighbouring biomes (if they exist)
    public void update_chunk_neighbours(bool also_neighboring_biomes = true)
    {
        if (also_neighboring_biomes)
            for (int dx = -1; dx < 2; ++dx)
                for (int dz = -1; dz < 2; ++dz)
                    get_neighbour(dx, dz, false)?.update_chunk_neighbours(false);

        else
            for (int i = 0; i < CHUNKS_PER_SIDE; ++i)
                for (int j = 0; j < CHUNKS_PER_SIDE; ++j)
                {
                    chunk_grid[i, j]?.terrain?.SetNeighbors(
                        extended_chunk_grid(i - 1, j, false)?.terrain,
                        extended_chunk_grid(i, j + 1, false)?.terrain,
                        extended_chunk_grid(i + 1, j, false)?.terrain,
                        extended_chunk_grid(i, j - 1, false)?.terrain
                    );
                }
    }

    // Returns true if this biome contains a chunk which
    // is active (i.e within render range)
    public bool contains_enabled_chunk()
    {
        for (int i = 0; i < CHUNKS_PER_SIDE; ++i)
            for (int j = 0; j < CHUNKS_PER_SIDE; ++j)
                if (chunk_grid[i, j].enabled)
                    return true;
        return false;
    }

    //################//
    // BIOME BLENDING //
    //################//

    void get_blend_amounts(
        Vector3 position, // The point at which to evaluate the blend amounts 
        out float xamt,   // Abs(xamt) = amount to blend, Sign(xamt) = x direction to blend
        out float zamt    // Abs(zamt) = amount to blend, Sign(zamt) = z direction to blend
        )
    {
        int i = Mathf.FloorToInt(position.x) - x * SIZE;
        int j = Mathf.FloorToInt(position.z) - z * SIZE;

        xamt = zamt = 0;

        // Create a linearly increasing blend at the edge of the biome
        if (i <= BLEND_DISTANCE) xamt = i / (float)BLEND_DISTANCE - 1f;
        else if (i >= SIZE - 1 - BLEND_DISTANCE) xamt = 1f - (SIZE - 1 - i) / (float)BLEND_DISTANCE;

        if (j <= BLEND_DISTANCE) zamt = j / (float)BLEND_DISTANCE - 1f;
        else if (j >= SIZE - 1 - BLEND_DISTANCE) zamt = 1f - (SIZE - 1 - j) / (float)BLEND_DISTANCE;

        // Smooth the linear blend out so it has zero gradient at the very edge of the biome
        if (xamt > 0) xamt = procmath.maps.smooth_max_cos(xamt);
        else if (xamt < 0) xamt = -procmath.maps.smooth_max_cos(-xamt);

        if (zamt > 0) zamt = procmath.maps.smooth_max_cos(zamt);
        else if (zamt < 0) zamt = -procmath.maps.smooth_max_cos(-zamt);
    }

    public point blended_point(Vector3 world_position)
    {
        // Get the x and z amounts to blend
        float xamt, zamt;
        get_blend_amounts(world_position, out xamt, out zamt);

        // We blend at most 4 points:
        //   one from this biome (guaranteed)
        //   one from the neighbour in the +/- x direction
        //   one from the neighbour in the +/- z direction
        //   one from the diaonal neighbour between the above two
        var points = new point[4];
        var weights = new float[4];

        points[0] = clamped_grid(world_position);
        weights[0] = 1.0f;

        // Blend in the neihbour in the +/- x direction
        float abs_x = Mathf.Abs(xamt);
        if (abs_x > 0)
        {
            points[1] = get_neighbour(xamt > 0 ? 1 : -1, 0).clamped_grid(world_position);
            weights[1] = abs_x;
        }

        // Blend in the neihbour in the +/- z direction
        float abs_z = Mathf.Abs(zamt);
        if (abs_z > 0)
        {
            points[2] = get_neighbour(0, zamt > 0 ? 1 : -1).clamped_grid(world_position);
            weights[2] = abs_z;
        }

        // Blend the neihbour in the diagonal direction
        float damt = Mathf.Min(abs_x, abs_z);
        if (damt > 0)
        {
            points[3] = get_neighbour(xamt > 0 ? 1 : -1, zamt > 0 ? 1 : -1).clamped_grid(world_position);
            weights[3] = damt;
        }

        return point.average(points, weights);
    }

    // Get a particular point in the biome grid in world
    // coordinates. Clamps the biome point values
    // outside the range of the biome.
    point clamped_grid(Vector3 world_position)
    {
        int i = Mathf.FloorToInt(world_position.x) - SIZE * x;
        int j = Mathf.FloorToInt(world_position.z) - SIZE * z;
        i = Mathf.Clamp(i, 0, SIZE - 1);
        j = Mathf.Clamp(j, 0, SIZE - 1);
        return grid[i, j];
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    private void Update()
    {
        // Load neighbours if they are in range
        for (int dx = -1; dx < 2; ++dx)
            for (int dz = -1; dz < 2; ++dz)
                if (in_range(x + dx, z + dz))
                    get_neighbour(dx, dz, true);

        // Save/destroy biomes that are no longer needed
        if (no_longer_needed())
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Remove this from the grid of generated biomes
        generated_biomes.remove(x, z);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(
            transform.position + new Vector3(1, 0, 1) * SIZE / 2f,
            new Vector3(SIZE, 0.01f, SIZE));
    }

    //##################//
    // BIOME GENERATION //
    //##################//

    static two_int_dictionary<biome> generated_biomes = 
        new two_int_dictionary<biome>();

    // Generates the biome at x, z
    public static biome generate(int x, int z)
    {
        // Create the biome random number generator, seeded 
        // by the biome x, z coords and the world seed
        System.Random rand = procmath.multiseed_random(x, z, world.seed);

        // Use the above random number generator to pick which biome to generate
        int i = rand.Next() % biome_list.Count;
        var b = (biome)biome_list[i].Invoke(null, new object[] { x, z, rand });
        generated_biomes.add(x, z, b);
        return b;
    }

    // Creation methods for all generated biome types
    static List<MethodInfo> _biome_list;
    static List<MethodInfo> biome_list
    {
        get
        {
            if (_biome_list == null)
            {
                // Find the biome types
                _biome_list = new List<MethodInfo>();
                var asem = Assembly.GetAssembly(typeof(biome));
                var types = asem.GetTypes();

                foreach (var t in types)
                {
                    // Check if this type is a valid biome
                    if (!t.IsSubclassOf(typeof(biome))) continue;
                    if (t.IsAbstract) continue;

                    // Get the create method
                    var method = typeof(biome).GetMethod("generate", BindingFlags.NonPublic | BindingFlags.Static);
                    var generate_method = method.MakeGenericMethod(t);

                    // If the world is named after a biome, only generate that biome
                    // (useful for testing)
                    if (t.Name == world.name)
                    {
                        // Enforce the biome override
                        _biome_list = new List<MethodInfo> { generate_method };
                        break;
                    }

                    // Get biome info, if it exists
                    var bi = (biome_info)t.GetCustomAttribute(typeof(biome_info));
                    if (bi != null)
                    {
                        if (!bi.generation_enabled)
                            continue; // Skip allowing this biome
                    }

                    _biome_list.Add(generate_method);
                }
            }
            return _biome_list;
        }
    }

    // Create a biome of the given type
    static T generate<T>(int x, int z, System.Random random) where T : biome
    {
        var b = new GameObject("biome_" + x + "_" + z).AddComponent<T>();
        b.transform.position = new Vector3(x, 0, z) * SIZE;

        b.random = random;
        b.x = x;
        b.z = z;

        // Initialize the chunk grid
        for (int i = 0; i < CHUNKS_PER_SIDE; ++i)
            for (int j = 0; j < CHUNKS_PER_SIDE; ++j)
            {
                // Get the chunk coordinates from my
                // coordinates and the chunk grid coordinates
                int cx = x * CHUNKS_PER_SIDE + i;
                int cz = z * CHUNKS_PER_SIDE + j;
                b.chunk_grid[i, j] = chunk.create(cx, cz, b.random.Next());
                b.chunk_grid[i, j].transform.SetParent(b.transform);
            }

        // Generate the biome
        var sw = System.Diagnostics.Stopwatch.StartNew();
        b.generate_grid();
        utils.log("Generated biome " + x + ", " + z +
                  " (" + b.GetType().Name + ") in " +
                  sw.ElapsedMilliseconds + " ms", "generation");
        return b;
    }

    //################//
    // BIOME DISPOSAL //
    //################//

    // Check if this biome is no longer required in-game
    bool no_longer_needed()
    {
        // If biome is in range, it's definately needed
        if (in_range(x, z)) return false;

        // If there is an enabled chunk in an adjacent
        // biome, we could still be needed for biome blending
        for (int dx = -1; dx < 2; ++dx)
            for (int dz = -1; dz < 2; ++dz)
                if (get_neighbour(dx, dz, false)?.contains_enabled_chunk() ?? false)
                    return false;

        // Definately not needed
        return true;
    }

    //#############//
    // BIOME.POINT //
    //#############//

    // Describes a particular point in the biome
    public class point
    {
        public const float BEACH_START = world.SEA_LEVEL + 1f;
        public const float BEACH_END = world.SEA_LEVEL + 2f;

        public float altitude;
        public Color terrain_color;
        public world_object object_to_generate;
        public object gen_info;

        // Computea a weighted average of a list of points
        public static point average(point[] pts, float[] wts)
        {
            point ret = new point
            {
                altitude = 0,
                terrain_color = new Color(0, 0, 0, 0)
            };

            float total_weight = 0;
            foreach (var f in wts) total_weight += f;

            int max_i = 0;
            float max_w = 0;
            for (int i = 0; i < wts.Length; ++i)
            {
                float w = wts[i] / total_weight;
                var p = pts[i];
                if (p == null) continue;

                ret.altitude += p.altitude * w;
                ret.terrain_color.r += p.terrain_color.r * w;
                ret.terrain_color.g += p.terrain_color.g * w;
                ret.terrain_color.b += p.terrain_color.b * w;

                if (wts[i] > max_w)
                {
                    max_w = wts[i];
                    max_i = i;
                }
            }

            if (pts[max_i] != null)
            {
                ret.object_to_generate = pts[max_i].object_to_generate;
                ret.gen_info = pts[max_i].gen_info;
            }

            return ret;
        }

        public void apply_global_rules()
        {
            // Enforce beach color
            if (altitude < BEACH_START)
                terrain_color = terrain_colors.sand;
            else if (altitude < BEACH_END)
            {
                float s = 1 - procmath.maps.linear_turn_on(altitude, BEACH_START, BEACH_END);
                terrain_color = Color.Lerp(terrain_color, terrain_colors.sand, s);
            }

            // Check if the object can be placed 
            // here, if not, remove it
            if (object_to_generate != null)
                if (!object_to_generate.can_place(this))
                    object_to_generate = null;
        }

        public string info()
        {
            return "Altitude: " + altitude + " Terrain color: " + terrain_color;
        }
    }
}

// Attribute info for biomes
public class biome_info : System.Attribute
{
    public bool generation_enabled { get; private set; }

    public biome_info(bool generation_enabled = true)
    {
        this.generation_enabled = generation_enabled;
    }
}

public enum COMPASS_DIRECTION
{
    NORTH,
    SOUTH,
    EAST,
    WEST
}

public class int_rect
{
    public int_rect(int left, int right, int bottom, int top)
    {
        this.left = left;
        this.right = right;
        this.bottom = bottom;
        this.top = top;
    }

    public int left { get; protected set; }
    public int bottom { get; protected set; }
    public int right { get; protected set; }
    public int top { get; protected set; }
    public int width { get => right - left; }
    public int height { get => top - bottom; }

    public bool is_edge(int edge_width, int x, int z)
    {
        return x > right - edge_width ||
               x < left + edge_width  ||
               z > top - edge_width   ||
               z < bottom + edge_width;
    }
}