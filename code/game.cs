using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class game : MonoBehaviour
{
    public const float MIN_RENDER_RANGE = 4f;
    public const string PLAYER_PREFAB = "misc/player";
    public const float SLOW_UPDATE_TIME = 0.1f;

    public Canvas main_canvas;
    public UnityEngine.UI.Text debug_text;
    public GameObject debug_panel;

    /// <summary> Information on how to start a game. </summary>
    public struct startup_info
    {
        public enum MODE
        {
            LOAD_AND_HOST,
            CREATE_AND_HOST,
            JOIN,
        }

        public MODE mode;
        public string username;
        public string world_name;
        public int world_seed;
        public string hostname;
        public int port;
    }
    public static startup_info startup;

    /// <summary> Load/host a game from disk. </summary>
    public static void load_and_host_world(string world_name, string username)
    {
        startup = new startup_info
        {
            username = username,
            mode = startup_info.MODE.LOAD_AND_HOST,
            world_name = world_name,
        };

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    /// <summary> Create/host a new world. </summary>
    public static void create_and_host_world(string world_name, int seed, string username)
    {
        startup = new startup_info
        {
            username = username,
            mode = startup_info.MODE.CREATE_AND_HOST,
            world_name = world_name,
            world_seed = seed,
        };

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    /// <summary> Join a world hosted on a server. </summary>
    public static bool join_world(string ip_port, string username)
    {
        string ip = ip_port.Split(':')[0];
        int port;
        if (!int.TryParse(ip_port.Split(':')[1], out port))
            return false;

        startup = new startup_info
        {
            username = username,
            mode = startup_info.MODE.JOIN,
            hostname = ip,
            port = port
        };

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
        return true;
    }

    /// <summary> The target render range, which the actual render range will lerp to. </summary>
    public static float render_range_target
    {
        get { return _render_range_target; }
        set
        {
            if (value < MIN_RENDER_RANGE) value = MIN_RENDER_RANGE;
            _render_range_target = value;
        }
    }
    private static float _render_range_target = chunk.SIZE;

    /// <summary> How far the player can see. </summary>
    public static float render_range
    {
        get { return _render_range; }
        private set
        {
            if (_render_range == value) return;
            if (value < MIN_RENDER_RANGE) value = MIN_RENDER_RANGE;
            _render_range = value;
            player.current.update_render_range();
            water_reflections.water_range = value;
        }
    }
    private static float _render_range = chunk.SIZE;

    void on_client_disconnect(string message_from_server)
    {
        // Stop the server if this was the host
        if (server.started)
            server.stop();

        // Go back to the world menu
        if (message_from_server != null)
            world_menu.message_to_display = "Disconnected: " + message_from_server;
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/world_menu");
    }

    void Start()
    {
        // Various startup modes
        switch (startup.mode)
        {
            case startup_info.MODE.LOAD_AND_HOST:
            case startup_info.MODE.CREATE_AND_HOST:

                // Start + join the server
                server.start(server.DEFAULT_PORT, startup.world_name, PLAYER_PREFAB);

                client.connect(network_utils.local_ip_address().ToString(),
                    server.DEFAULT_PORT, startup.username, "password", on_client_disconnect);

                // Create the world (if required)
                if (startup.mode == startup_info.MODE.CREATE_AND_HOST)
                {
                    var w = (world)client.create(Vector3.zero, "misc/world");
                    w.networked_seed.value = startup.world_seed;
                    w.networked_name.value = startup.world_name;

                    // Create the various always-loaded objects
                    client.create(Vector3.zero, "misc/time_manager");
                    client.create(Vector3.zero, "misc/teleport_manager");
                }

                break;

            case startup_info.MODE.JOIN:

                // Join the server
                client.connect(startup.hostname, startup.port,
                    startup.username, "password", on_client_disconnect);

                break;

            default:
                throw new System.Exception("Unkown startup mode!");
        }

        // Start with invisible, locked cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        //if (Application.isEditor)
        QualitySettings.vSyncCount = 0;

        // Ensure we're using SRP batching
        UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching = true;

        // Debug panel starts closed
        debug_panel.SetActive(false);

        // Initialize various things
        options_menu.initialize();
        item_link_point.init_links();
        bed.initialize_beds();
        biome.initialize();

        // Set the slow_update method going
        InvokeRepeating("slow_update", 0, SLOW_UPDATE_TIME);

        // Set the network updates going
        InvokeRepeating("network_update", 1f / 60f, 1f / 60f);
    }

    void Update()
    {
        // Update load balancing info
        load_balancing.update();

        // Toggle the console
        if (controls.key_press(controls.BIND.TOGGLE_CONSOLE))
            console.open = !console.open;

        // Toggle the debug panel
        if (controls.key_press(controls.BIND.TOGGLE_DEBUG_INFO))
            debug_panel.SetActive(!debug_panel.activeInHierarchy);

        // Increase/Decrease render ranges
        if (controls.key_press(controls.BIND.INCREASE_RENDER_RANGE)) render_range_target += 10f;
        if (controls.key_press(controls.BIND.DECREASE_RENDER_RANGE)) render_range_target -= 10f;
        render_range = Mathf.Lerp(render_range, render_range_target, 3 * Time.deltaTime);

        // Spawn enemies
        enemies.update();
    }

    void network_update()
    {
        // Run networking updates
        server.update();
        client.update();
    }

    /// <summary> Called every <see cref="SLOW_UPDATE_TIME"/> seconds. </summary>
    void slow_update()
    {
        if (!debug_panel.activeInHierarchy)
            return;

        string debug_text = "" +
            "\nVERSION\n"+
            version_control.info() + "\n" +
            "\nWORLD\n" +
            world.info() + "\n" +
            "\nGRAPHICS\n" +
            graphics_info() + "\n" +
            "\nSERVER\n" +
            server.info() + "\n" +
            "\nCLIENT\n" +
            client.info() + "\n" +
            "\nPLAYER\n" +
            player.info() + "\n" +
            "\nLOAD BALANCER\n" +
            load_balancing.info() + "\n" +
            "\nENEMY SPAWNING\n" +
            enemies.info() + "\n" +
            "\nTIME OF DAY\n"+
            time_manager.info() +"\n";

        debug_text = debug_text.Trim();
        this.debug_text.text = utils.allign_colons(debug_text);
    }

    private void OnDrawGizmos()
    {
        enemies.draw_gizmos();
    }

    void OnApplicationQuit()
    {
        // Disconnect from the network
        client.disconnect(true);
        server.stop();
    }

    public string graphics_info()
    {
        return "    FPS             : " + System.Math.Round(1 / Time.deltaTime, 0) + "\n" +
               "    Fullscreen mode : " + Screen.fullScreenMode + "\n" +
               "    Resolution      : " + Screen.width + " x " + Screen.height;
    }
}
