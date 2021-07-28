using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// A biome represents the largest singly-generated area of 
// the map and is used to generate quantities that exist on 
// the largest length scales, such as the terrain.
public abstract class biome : MonoBehaviour
{
    /// <summary> The distance at the edge of a biome that is blended.
    /// Set to slightly less than a chunk so we only need blend the 
    /// outermost chunks in a biome. </summary>
    public const int TERRAIN_BLEND_DISTANCE = chunk.SIZE - 1;
    public const int OBJECT_BLEND_DISTANCE = TERRAIN_BLEND_DISTANCE / 8;

    /// <summary> Biome x coordinate in the world. </summary>
    public int x { get; private set; }

    /// <summary> Biome z coordinate in the world. </summary>
    public int z { get; private set; }

    /// <summary> The random number generator specific to this biome. </summary>
    public System.Random random { get; private set; }

    /// <summary> Perlin noise, with a pattern specific to this biome. </summary>
    protected float perlin(float x, float y)
    {
        return Mathf.PerlinNoise(x + this.x * 10f, y + this.z * 10f);
    }

    /// <summary> The grid of points defining the biome. </summary>
    public point[,] grid = new point[SIZE, SIZE];
    protected abstract bool continue_generate_grid();

    /// <summary> Called just before the first call to <see cref="continue_generate_grid"/> </summary>
    protected virtual void on_start_generate_grid() { }

    /// <summary> Called just after the last call to <see cref="continue_generate_grid"/> </summary>
    protected virtual void on_end_generate_grid() { }

    /// <summary> True once generation is complete. </summary>
    public bool generation_complete { get; private set; }

    public biome_modifier modifier { get; private set; }

    //#####################//
    // NEIGHBOURING BIOMES //
    //#####################//

    /// <summary> Get a neighbouring biome with biome coorindates 
    /// <see cref="x"/>+<paramref name="dx"/>,
    /// <see cref="z"/>+<paramref name="dz"/>. </summary>
    /// <param name="generate_if_needed">True if we should generate 
    /// the neighbouring biome if it doesn't already exist.</param>
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
    biome[,] _neihbours = new biome[3, 3];

    //##################//
    // COORDINATE TOOLS //
    //##################//

    /// <summary> Get the biome coords at a given world location. </summary>
    public static int[] coords(Vector3 location)
    {
        return new int[]
        {
            Mathf.FloorToInt(location.x / SIZE),
            Mathf.FloorToInt(location.z / SIZE)
        };
    }

    /// <summary> Returns the chunk at the given location, if it has 
    /// been generated. Null otherwise. </summary>
    public static biome at(Vector3 location, bool generate = false)
    {
        var c = coords(location);
        var found = generated_biomes?.get(c[0], c[1]);
        if (found != null) return found;
        else if (!generate) return null;
        return biome.generate(c[0], c[1]);
    }

    /// <summary> Check if the biome at x, z is within render range
    /// (essentially testing if the render range circle 
    ///  intersects the biome square). </summary>
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

    /// <summary> Gives the nearest coordinates to the given world 
    /// position, in my grid of biome points. </summary>
    public void get_grid_coords(Vector3 world_position, out int i, out int j)
    {
        Vector3 delta = world_position - transform.position;
        i = Mathf.Clamp(0, Mathf.FloorToInt(delta.x), SIZE);
        j = Mathf.Clamp(0, Mathf.FloorToInt(delta.z), SIZE);
    }

    //#########################//
    // CHUNK GRID MANIPULATION //
    //#########################//

    /// <summary> The size of a biome in chunks per side. </summary>
    public const int CHUNKS_PER_SIDE = 3;

    /// <summary> The size of a biome in meters. </summary>
    public const int SIZE = CHUNKS_PER_SIDE * chunk.SIZE;

    /// <summary> The grid of chunks within the biome. </summary>
    chunk[,] chunk_grid = new chunk[CHUNKS_PER_SIDE, CHUNKS_PER_SIDE];

    /// <summary> The grid of chunks with coordinates 
    /// extended to include neighbouring biomes. </summary>
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

    /// <summary> Returns true if this biome contains a chunk which
    /// is active (i.e within render range). </summary>
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
        float blend_distance, // The distance from the edge of the biome that blending starts
        out float xamt,   // Abs(xamt) = amount to blend, Sign(xamt) = x direction to blend
        out float zamt    // Abs(zamt) = amount to blend, Sign(zamt) = z direction to blend
        )
    {
        int i = Mathf.FloorToInt(position.x) - x * SIZE;
        int j = Mathf.FloorToInt(position.z) - z * SIZE;

        xamt = zamt = 0;

        // Create a linearly increasing blend at the edge of the biome
        if (i <= blend_distance) xamt = i / (float)blend_distance - 1f;
        else if (i >= SIZE - 1 - blend_distance) xamt = 1f - (SIZE - 1 - i) / (float)blend_distance;

        if (j <= blend_distance) zamt = j / (float)blend_distance - 1f;
        else if (j >= SIZE - 1 - blend_distance) zamt = 1f - (SIZE - 1 - j) / (float)blend_distance;

        // Smooth the linear blend out so it has zero gradient at the very edge of the biome
        if (xamt > 0) xamt = procmath.maps.smooth_max_cos(xamt);
        else if (xamt < 0) xamt = -procmath.maps.smooth_max_cos(-xamt);

        if (zamt > 0) zamt = procmath.maps.smooth_max_cos(zamt);
        else if (zamt < 0) zamt = -procmath.maps.smooth_max_cos(-zamt);
    }

    [test_method]
    static bool test_blend_amounts()
    {
        var b = player.current.biome;
        b.get_blend_amounts(player.current.transform.position, TERRAIN_BLEND_DISTANCE, out float xamt, out float zamt);
        if (test_method.running_interactive)
            Debug.Log("X blend: " + xamt + ", Z blend: " + zamt);
        return true;
    }

    /// <summary> Returns a blended <see cref="biome.point"/> at the given <paramref name="world_position"/>.
    /// <paramref name="valid"/> will be set to false if any of the biomes required for blending
    /// are not yet generated, to let the calling method know to try again later. </summary>
    public point blended_point(Vector3 world_position, out bool valid)
    {
        if (!generation_complete)
        {
            valid = false;
            return new point();
        }

        // Get the x and z amounts to blend
        get_blend_amounts(world_position, TERRAIN_BLEND_DISTANCE, out float xamt_ter, out float zamt_ter);
        get_blend_amounts(world_position, OBJECT_BLEND_DISTANCE, out float xamt_obj, out float zamt_obj);

        // We blend at most 4 points:
        //   one from this biome (guaranteed)
        //   one from the neighbour in the +/- x direction
        //   one from the neighbour in the +/- z direction
        //   one from the diaonal neighbour between the above two
        var object_points = new point[4];
        var terrain_points = new point[4];
        var terrain_weights = new float[4];
        var object_weights = new float[4];

        object_points[0] = periodic_grid(world_position);
        terrain_points[0] = clamped_grid(world_position);
        terrain_weights[0] = 1f;
        object_weights[0] = 1f;

        // Blend in the neihbour in the +/- x direction
        float abs_x = Mathf.Abs(xamt_ter);
        if (abs_x > 0)
        {
            var nx = get_neighbour(xamt_ter > 0 ? 1 : -1, 0);

            if (!nx.generation_complete)
            {
                valid = false;
                return new point();
            }

            object_points[1] = nx.periodic_grid(world_position);
            terrain_points[1] = nx.clamped_grid(world_position);
            terrain_weights[1] = abs_x;
            object_weights[1] = Mathf.Abs(xamt_obj);
        }

        // Blend in the neihbour in the +/- z direction
        float abs_z = Mathf.Abs(zamt_ter);
        if (abs_z > 0)
        {
            var nz = get_neighbour(0, zamt_ter > 0 ? 1 : -1);

            if (!nz.generation_complete)
            {
                valid = false;
                return new point();
            }

            object_points[2] = nz.periodic_grid(world_position);
            terrain_points[2] = nz.clamped_grid(world_position);
            terrain_weights[2] = abs_z;
            object_weights[2] = Mathf.Abs(zamt_obj);
        }

        // Blend the neihbour in the diagonal direction
        float damt = Mathf.Min(abs_x, abs_z);
        if (damt > 0)
        {
            var nd = get_neighbour(xamt_ter > 0 ? 1 : -1, zamt_ter > 0 ? 1 : -1);

            if (!nd.generation_complete)
            {
                valid = false;
                return new point();
            }

            object_points[3] = nd.periodic_grid(world_position);
            terrain_points[3] = nd.clamped_grid(world_position);
            terrain_weights[3] = damt;
            object_weights[3] = Mathf.Min(Mathf.Abs(xamt_obj), Mathf.Abs(zamt_obj));
        }

        valid = true;
        var rand = procmath.multiseed_random((int)world_position.x, (int)world_position.z);
        return point.blend(terrain_points, object_points, terrain_weights, object_weights, (float)rand.NextDouble());
    }

    static int ping_pong_coord(int i)
    {
        if (i < 0) return ping_pong_coord(-i);
        if ((i / SIZE) % 2 == 0) return utils.positive_mod(i, SIZE);
        return SIZE - utils.positive_mod(i, SIZE) - 1;
    }

    [test_method]
    public static bool test_ping_pong()
    {
        bool ret = true;
        bool first = true;
        int pp_last = 0;
        string s = "";
        for (int i = -3 * SIZE; i < 3 * SIZE; ++i)
        {
            int pp = ping_pong_coord(i);
            if (!first && Mathf.Abs(pp - pp_last) > 1)
                ret = false;
            pp_last = pp;
            first = false;
            s += pp + ", ";
        }
        if (test_method.running_interactive)
            Debug.Log(s);
        return ret;
    }

    [test_method]
    public static bool test_ping_pong_here()
    {
        Vector3 world_position = player.current.transform.position;
        int xp = Mathf.FloorToInt(world_position.x);
        int zp = Mathf.FloorToInt(world_position.z);
        int i = ping_pong_coord(xp);
        int j = ping_pong_coord(zp);
        if (test_method.running_interactive)
            Debug.Log("Ping pong coords at " + xp + ", " + zp + " = " + i + ", " + j);
        return true;
    }

    /// <summary> Get a particular point in the biome grid in world coordinates. 
    /// Applies (reflected) periodic boundary conditions to the grid. </summary>
    point periodic_grid(Vector3 world_position)
    {
        int i = ping_pong_coord(Mathf.FloorToInt(world_position.x));
        int j = ping_pong_coord(Mathf.FloorToInt(world_position.z));
        return grid[i, j];
    }

    /// <summary> Get a particular point in the biome grid in world coordinates.
    /// Applies clamped boundary conditions. </summary>
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
        if (!console.world_generator_enabled) return;

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
        generated_biomes.clear(x, z);

        // Trigger removal of unused assets, to try to
        // reduce memory usage
        Resources.UnloadUnusedAssets();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(
            transform.position + new Vector3(1, 0, 1) * SIZE / 2f,
            new Vector3(SIZE, 0.01f, SIZE));
    }

    //##################//
    // BIOME GENERATION //
    //##################//

    static Dictionary<int, int, biome> generated_biomes;
    static List<MethodInfo> biome_list;
    static List<MethodInfo> modifier_list;

    public static int active_biomes => biome_list == null ? 0 : biome_list.Count;
    public static string nth_biome(int n) => biome_list[n].ReturnType.Name;

    public static void initialize()
    {
        // Ensure the biome list doesn't persist from previous game
        biome_list = null;
        modifier_list = null;
    }

    /// <summary> Initialize static information ready for world generation. </summary>
    public static void generate_biome_list()
    {
        // Initialize static variables
        generated_biomes = new Dictionary<int, int, biome>();
        biome_list = new List<MethodInfo>();
        modifier_list = new List<MethodInfo>();

        var asem = Assembly.GetAssembly(typeof(biome));
        var types = asem.GetTypes();

        string biome_override = world.name;
        string modifier_override = world.name;

        if (world.name.Contains("+"))
        {
            biome_override = world.name.Split('+')[0];
            modifier_override = world.name.Split('+')[1];
        }

        // Generate the biome list
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
            if (t.Name == biome_override)
            {
                // Enforce the biome override
                biome_list = new List<MethodInfo> { generate_method };
                break;
            }

            // Get biome info, if it exists
            var bi = (biome_info)t.GetCustomAttribute(typeof(biome_info));
            if (bi != null)
            {
                if (!bi.generation_enabled)
                    continue; // Skip allowing this biome
            }

            biome_list.Add(generate_method);
        }

        // Generate the modifier list
        foreach (var t in types)
        {
            if (!t.IsSubclassOf(typeof(biome_modifier))) continue;
            if (t.IsAbstract) continue;

            // Get the modifier construction method
            var method = typeof(biome_modifier).GetMethod("generate", BindingFlags.NonPublic | BindingFlags.Static);
            var generate_method = method.MakeGenericMethod(t);

            if (t.Name == modifier_override)
            {
                // Enforce the modifier override
                modifier_list = new List<MethodInfo> { generate_method };
                break;
            }

            var bmi = (biome_mod_info)t.GetCustomAttribute(typeof(biome_mod_info));
            if (bmi != null)
            {
                if (!bmi.generation_enabled)
                    continue; // Skip allowing this modifier
            }

            modifier_list.Add(generate_method);
        }
    }

    static System.Random get_biome_rand_gen(int x, int z)
    {
        // Create the biome random number generator, seeded 
        // by the biome x, z coords and the world seed
        return procmath.multiseed_random(x, z, world.seed);
    }

    public static System.Type peek_biome_type(Vector3 world_position)
    {
        var c = coords(world_position);
        return peek_biome_type(c[0], c[1]);
    }

    public static System.Type peek_biome_type(int x, int z)
    {
        // Get the random number generator used to pick biomes
        var rand = get_biome_rand_gen(x, z);
        int i = rand.Next() % biome_list.Count;
        return biome_list[i].ReturnType;
    }

    /// <summary> Generates the biome with the given biome coordinates. </summary>
    public static biome generate(int x, int z)
    {
        // Ensure the biomes are loaded
        if (biome_list == null)
            generate_biome_list();

        // Get the random number generator used to pick biomes
        var rand = get_biome_rand_gen(x, z);

        biome b;
        if (x == 0 && z == 0 && biome_list.Count > 1)
        {
            // Start in the tutorial island (if the biome override isn't in effect)
            b = generate<tutorial_island>(0, 0, rand);
            b.modifier = new no_modifier();
            b.name = "[0, 0] tutorial island";
        }
        else
        {
            // Use the above random number generator to pick which biome to generate
            int i = rand.Next() % biome_list.Count;
            b = (biome)biome_list[i].Invoke(null, new object[] { x, z, rand });

            // Use the above random number generator to pick a random biome modifier
            i = rand.Next() % modifier_list.Count;
            b.modifier = (biome_modifier)modifier_list[i].Invoke(null, new object[] { });

            // Give the biome a discriptive name
            b.name = "[" + x + ", " + z + "] " + b.GetType().Name + " + " + b.modifier.GetType().Name;
        }

        generated_biomes.set(x, z, b);
        return b;
    }

    /// <summary> Generate a biome of the given type + coordinates. </summary>
    static T generate<T>(int x, int z, System.Random random) where T : biome
    {
        var b = new GameObject().AddComponent<T>();
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
        var bg = b.gameObject.AddComponent<gradual_biome_generator>();
        bg.biome = b;
        return b;
    }

    /// <summary> Class to spread biome generation across multiple frames. </summary>
    class gradual_biome_generator : MonoBehaviour
    {
        public biome biome;

        private void Start()
        {
            biome.on_start_generate_grid();
        }

        private void Update()
        {
            for (int i = 0; i < load_balancing.iter * 4; ++i)
                if (biome.continue_generate_grid())
                {
                    biome.on_end_generate_grid();
                    biome.modifier.modify(biome); // Apply the biome modifier
                    biome.generation_complete = true;
                    Destroy(this);
                    return;
                }
        }
    }

    //################//
    // BIOME DISPOSAL //
    //################//

    /// <summary> Check if this biome is no longer required in game. </summary>
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

    /// <summary> Describes a particular point within a biome. </summary>
    public class point
    {
        public const float BEACH_START = world.SEA_LEVEL + 1f;
        public const float BEACH_END = world.SEA_LEVEL + 2f;

        public float altitude;
        public float fog_distance;
        public Color water_color = water_colors.cyan;
        public Color terrain_color = terrain_colors.grass;
        public Color sky_color = sky_colors.light_blue;
        public Color beach_color = terrain_colors.sand;
        public world_object object_to_generate;

        /// <summary> Compute a weighted average of a list of points. </summary>
        public static point blend(point[] terrain_points, point[] object_points, float[] terrain_weights, float[] object_weights, float random_number)
        {
            point ret = new point
            {
                altitude = 0,
                terrain_color = new Color(0, 0, 0, 0),
                sky_color = new Color(0, 0, 0, 1),
                water_color = new Color(0, 0, 0, 0),
                beach_color = new Color(0, 0, 0, 0)
            };

            // Work out total terrain and object weights
            float total_terrain_weight = 0;
            float total_object_weight = 0;
            for (int i = 0; i < terrain_weights.Length; ++i)
            {
                total_terrain_weight += terrain_weights[i];
                total_object_weight += object_weights[i];
            }

            // Weight terrain data from terrain points
            for (int i = 0; i < terrain_weights.Length; ++i)
            {
                var tp = terrain_points[i];
                if (tp == null) continue;
                float w = terrain_weights[i] / total_terrain_weight;

                ret.altitude += tp.altitude * w;
                ret.fog_distance += tp.fog_distance * w;

                ret.terrain_color.r += tp.terrain_color.r * w;
                ret.terrain_color.g += tp.terrain_color.g * w;
                ret.terrain_color.b += tp.terrain_color.b * w;

                ret.sky_color.r += tp.sky_color.r * w;
                ret.sky_color.g += tp.sky_color.g * w;
                ret.sky_color.b += tp.sky_color.b * w;

                ret.water_color.r += tp.water_color.r * w;
                ret.water_color.g += tp.water_color.g * w;
                ret.water_color.b += tp.water_color.b * w;

                ret.beach_color.r += tp.beach_color.r * w;
                ret.beach_color.g += tp.beach_color.g * w;
                ret.beach_color.b += tp.beach_color.b * w;
            }

            // Weight object data from object points
            random_number = Mathf.Clamp(random_number * total_object_weight, 0, total_object_weight);
            float cumulative = 0;
            for (int i = 0; i < object_weights.Length; ++i)
            {
                cumulative += object_weights[i];
                if (cumulative > random_number)
                {
                    if (object_points[i] != null)
                        ret.object_to_generate = object_points[i].object_to_generate;
                    break;
                }
            }

            ret.terrain_color.a = 0f;
            return ret;
        }

        /// <summary> These rules will be applied to points just 
        /// before they are used in chunk generation. </summary>
        public void apply_global_rules()
        {
            // Enforce beach color
            if (altitude < BEACH_START)
                terrain_color = beach_color;
            else if (altitude < BEACH_END)
            {
                float s = 1 - procmath.maps.linear_turn_on(altitude, BEACH_START, BEACH_END);
                terrain_color = Color.Lerp(terrain_color, beach_color, s);
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

/// <summary> Attribute info for biomes. </summary>
public class biome_info : System.Attribute
{
    public bool generation_enabled { get; private set; }

    public biome_info(bool generation_enabled = true)
    {
        this.generation_enabled = generation_enabled;
    }
}

/// <summary> Attribute info for biome modifiers. </summary>
public class biome_mod_info : System.Attribute
{
    public bool generation_enabled { get; private set; }

    public biome_mod_info(bool generation_enabled = true)
    {
        this.generation_enabled = generation_enabled;
    }
}

/// <summary> A class that is used to create a 
/// modified version of a biome. </summary>
public abstract class biome_modifier
{
    public abstract void modify(biome b);

    static T generate<T>() where T : biome_modifier, new()
    {
        return new T();
    }
}