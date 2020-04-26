using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class game : MonoBehaviour
{
    public const float MIN_RENDER_RANGE = 0f;

    public static player player { get; private set; }

    public UnityEngine.UI.Text debug_text;

    public static void load_and_host_world(string path)
    {
        // Start the server + client
        networked.server.start(6969);
        networked.client.connect_to_server(
            networked.server.local_ip_address().ToString(),
            networked.server.port);

        // Create the world object
        DontDestroyOnLoad(networked.section.create<world>(
            int.Parse(System.IO.File.ReadAllText(path + "/seed")),
            path.Split('/')[path.Split('/').Length - 1]
        ));

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    public static void join_world()
    {
        networked.client.connect_to_server(
            networked.server.local_ip_address().ToString(),
            6969);

        // Create the world object
        DontDestroyOnLoad(networked.section.create<world>(0, "unloaded!"));
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    public static void create_and_host_world(string name, int seed)
    {
        // Start the server
        networked.server.start(6969);
        networked.client.connect_to_server(
            networked.server.local_ip_address().ToString(),
            networked.server.port);

        // Create the world object
        DontDestroyOnLoad(networked.section.create<world>(
            seed, name
        ));

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    // The target render range, which the actual render range will lerp to
    private static float _render_range_target = chunk.SIZE;
    public static float render_range_target
    {
        get { return _render_range_target; }
        set
        {
            if (value < MIN_RENDER_RANGE) value = MIN_RENDER_RANGE;
            _render_range_target = value;
        }
    }

    // How far the player can see
    private static float _render_range = chunk.SIZE;
    public static float render_range
    {
        get { return _render_range; }
        private set
        {
            if (_render_range == value) return;
            if (value < MIN_RENDER_RANGE) value = MIN_RENDER_RANGE;
            _render_range = value;
            player.update_render_range();
        }
    }

    void Start()
    {
        // Start with invisible, locked cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Create the player
        player = player.create();

        // Create the sky!
        create_sky();

        if (Application.isEditor)
            QualitySettings.vSyncCount = 0;
    }

    void create_sky()
    {
        // Create the sun
        var sun = new GameObject("sun").AddComponent<Light>();
        sun.transform.position = Vector3.zero;
        sun.transform.LookAt(new Vector3(1, -2, 1));
        sun.type = LightType.Directional;
        sun.intensity = 1f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.ambientSkyColor = new Color(0.3f, 0.3f, 0.3f);
    }

    bool generator_started = false;
    void start_generator()
    {
        // Create the biome at the players location
        if (generator_started) throw new System.Exception("Tried to start generator more than once!");
        generator_started = true;
        var biome_coords = biome.coords(player.current.transform.position);
        biome.generate(biome_coords[0], biome_coords[1]);
    }


    void Update()
    {
        if (!generator_started && world.loaded)
            start_generator();

        debug_text.text = "";

        debug_text.text += "World: " + world.name + " (seed " + world.seed + ")\n";

        if (networked.server.started)
        {
            networked.server.update();
            debug_text.text += networked.server.info();
        }

        if (networked.client.connected)
        {
            networked.client.update();
            debug_text.text += "\n" + networked.client.info();
        }

        if (Input.GetKeyDown(KeyCode.Equals)) render_range_target += 10f;
        if (Input.GetKeyDown(KeyCode.Minus)) render_range_target -= 10f;
        render_range = Mathf.Lerp(render_range, render_range_target, 3 * Time.deltaTime);
    }

    void OnApplicationQuit()
    {
        // Save the player
        player.current.save();

        // Save the world seed
        System.IO.File.WriteAllText(world.save_folder() + "/seed", "" + world.seed);

        if (networked.client.connected) networked.client.disconnect();
        if (networked.server.started) networked.server.stop();
    }
}
