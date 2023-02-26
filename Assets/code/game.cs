using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class game : MonoBehaviour
{
    public const float MIN_RENDER_RANGE = 4f;
    public const float SLOW_UPDATE_TIME = 0.1f;
    public const float CHARACTER_SPAWN_INTERVAL = 0.5f;
    public const int LOADING_TARGET_FRAMERATE = 10;
    public const int MAX_FRAMERATE = 240;

    public Canvas main_canvas;
    public UnityEngine.UI.Text debug_text;
    public UnityEngine.UI.Text controls_debug_text;
    public UnityEngine.UI.Text cursor_text_element;
    public GameObject debug_panel;
    public GameObject loading_message;
    public GameObject controls_debug_panel;

    static game()
    {
        // Generate community-provided documentation
        community_documentation.generate();
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    void Start()
    {
        // Implement the "cancel" button on the world loading screen
        loading_message.GetComponentInChildren<UnityEngine.UI.Button>().onClick.AddListener(() =>
        {
            client.disconnect(true, msg_from_server: "World loading cancelled");
        });

        // Get static reference to canvas
        canvas = main_canvas;
        cursor_text_static = cursor_text_element;

        // Attempt to connect the client to a server
        if (!client_connect()) return;

        // Ensure we're using SRP batching
        UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching = true;

        // Debug panel starts closed
        debug_panel.SetActive(false);
        controls_debug_panel.SetActive(false);

        // Initialize various things
        names.initialize();
        pinned_message.initialize();
        item_node.initialize();
        biome.initialize();
        chunk.initialize();
        town_path_element.initialize();
        character_spawn_point.initialize();
        character.initialize();
        settler.initialize();
        town_gate.initialize();
        attacker_entrypoint.initialize();
        options_menu.initialize(); // This relies on the most stuff, so should be initialized last

        // Set the slow_update method going
        InvokeRepeating("slow_update", 0, SLOW_UPDATE_TIME);

        // Set the character spawning going
        InvokeRepeating("character_spawn_update",
            CHARACTER_SPAWN_INTERVAL, CHARACTER_SPAWN_INTERVAL);
    }

    public static void create_manager_objects()
    {
        client.create(Vector3.zero, "misc/tech_tree");
        client.create(Vector3.zero, "misc/time_manager");
        client.create(Vector3.zero, "misc/teleport_manager");
        client.create(Vector3.zero, "misc/weather_manager");
    }

    bool client_connect()
    {
        // Various startup modes
        startup.validate();
        switch (startup.mode)
        {
            case startup_info.MODE.LOAD_AND_HOST:
            case startup_info.MODE.CREATE_AND_HOST:

                // Start + join the server
                if (!server.start(startup.world_name, "misc/player", out string error_message))
                {
                    am_hard_disconnecting = true;
                    on_client_disconnect(error_message);
                    return false;
                }

                // Connect to the server we just started
                client.connect_local(startup.username, startup.user_id, on_client_disconnect);

                // Create the world (if required)
                if (startup.mode == startup_info.MODE.CREATE_AND_HOST)
                {
                    var w = (world)client.create(Vector3.zero, "misc/world");
                    w.networked_seed.value = startup.world_seed;
                    w.networked_name.value = startup.world_name;

                    // Create the various always-loaded objects
                    create_manager_objects();
                }

                return true;

            case startup_info.MODE.JOIN:

                // Join the server
                client.direct_connect(startup.hostname, startup.port, startup.username, startup.user_id, on_client_disconnect);
                return true;

#if FACEPUNCH_STEAMWORKS
            case startup_info.MODE.JOIN_STEAM:

                // Join a steam friend
                client.steam_connect(startup.id_to_join, startup.username, startup.user_id, on_client_disconnect);
                return true;
#endif

            default:
                throw new System.Exception("Unkown startup mode!");
        }
    }

    void Update()
    {
        // Update load balancing info
        load_balancing.update();

        // Run network updates
        server.update();
        client.update();

        // Run various sub-system updates
        town_path_element.static_update();
        settler.static_update();

        // Open/Close the console
        if (controls.triggered(controls.BIND.OPEN_CONSOLE)) console.open = true;
        if (controls.triggered(controls.BIND.CLOSE_CONSOLE)) console.open = false;

        // Repeat the last console command
        if (controls.triggered(controls.BIND.REPEAT_LAST_CONSOLE_COMMAND))
            console.repeat_last_command();

        // Toggle the debug panels
        if (controls.triggered(controls.BIND.TOGGLE_DEBUG_INFO))
            debug_panel.SetActive(!debug_panel.activeInHierarchy);
        if (controls.triggered(controls.BIND.TOGGLE_CONTROL_DEBUG_INFO))
            controls_debug_panel.SetActive(!controls_debug_panel.activeInHierarchy);

        // Increase/Decrease render ranges
        if (controls.triggered(controls.BIND.INCREASE_RENDER_RANGE)) render_range_target += 10f;
        if (controls.triggered(controls.BIND.DECREASE_RENDER_RANGE)) render_range_target -= 10f;
        render_range = Mathf.Lerp(render_range, render_range_target, 3 * Time.deltaTime);

        run_loading_screen();
    }

    void run_loading_screen()
    {
        if (loading)
        {
            controls.disabled = true;
            loading_message.SetActive(true);

            // Ensure loading message is above everything
            // except for the debug panel
            loading_message.transform.SetAsLastSibling();
            debug_panel.transform.SetAsLastSibling();

            char[] symbs = new char[]
            {
                '|','/','-','\\','|','/','-','\\'
            };

            string t = "Loading world " + symbs[(int)(Time.time * LOADING_TARGET_FRAMERATE) % symbs.Length];

            var txt = loading_message.GetComponentInChildren<UnityEngine.UI.Text>();
            txt.text = t;
        }
        else if (loading_message.activeInHierarchy)
        {
            controls.disabled = false;
            loading_message.gameObject.SetActive(false);
            on_load?.Invoke();
            on_load = null;
        }
    }

    /// <summary> Called every <see cref="SLOW_UPDATE_TIME"/> seconds. </summary>
    void slow_update()
    {
        if (debug_panel.activeInHierarchy)
            debug_text.text = utils.allign_colons((
                "\nVERSION\n" +
                version_control.info.formatted() + "\n" +
                "\nWORLD\n" +
                world.info() + "\n" +
                "\nGRAPHICS\n" +
                graphics_info() + "\n" +
                "\nSERVER\n" +
                server.info() + "\n" +
                "\nCLIENT\n" +
                client.info() + "\n" +
                "\nPLAYERS\n" +
                player.info() + "\n" +
                client.connected_player_info() + "\n" +
                "\nCHARACTERS\n" +
                character.info() + "\n" +
                "\nSETTLERS\n" +
                settler.info() + "\n" +
                "\nINTERACTIONS\n" +
                character_interactable.info() + "\n" +
                "\nLOAD BALANCER\n" +
                load_balancing.info() + "\n" +
                "\nTIME OF DAY\n" +
                time_manager.info() + "\n"
            ).Trim());

        if (controls_debug_panel.activeInHierarchy)
            controls_debug_text.text = controls.debug_info();
    }

    void character_spawn_update()
    {
        character.run_spawning();
    }

    void OnApplicationQuit()
    {
        am_hard_disconnecting = true;
        save_and_quit();
    }

    /// <summary> True if we don't want/don't have 
    /// time to wait before closing the server. </summary>
    bool am_hard_disconnecting = false;
    string hard_disconnect_message = "";

    void hard_disconnect()
    {
        am_hard_disconnecting = true;
        on_client_disconnect(hard_disconnect_message);
    }

    void on_client_disconnect(string message_from_server)
    {
        if (server.started)
        {
            if (am_hard_disconnecting)
                server.stop();
            else
            {
                // Don't close the server for a little bit so we can
                // process messages that were sent during the disconnect
                // (for example, the DISCONNECT message from the player)
                hard_disconnect_message = message_from_server;
                Invoke("hard_disconnect", 1f);
                return;
            }
        }

        // Go back to the world menu
        if (message_from_server != null)
            world_menu.message_to_display = "Disconnected: " + message_from_server;

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/world_menu");
    }

    private void OnDrawGizmos()
    {
        town_path_element.draw_group_gizmos();
    }

    //##############//
    // STATIC STUFF //
    //##############//

    public static bool loading => player.current == null || !player.current.controller_enabled;

    public delegate void load_delegate();
    static load_delegate on_load;
    public static void call_when_loaded(load_delegate l)
    {
        if (loading) l?.Invoke();
        else on_load += l;
    }

    public static Canvas canvas { get; private set; }

    public static string cursor_text
    {
        get => cursor_text_static?.text;
        set
        {
            if (cursor_text_static != null)
                cursor_text_static.text = value;
        }
    }
    static UnityEngine.UI.Text cursor_text_static;

    /// <summary> Information on how to start a game. </summary>
    public struct startup_info
    {
        public enum MODE
        {
            LOAD_AND_HOST,
            CREATE_AND_HOST,
            JOIN,
            JOIN_STEAM,
        }

        public MODE mode;

        public string username;
        public ulong user_id;

        public string world_name;
        public int world_seed;

        public string hostname;
        public int port;

#if FACEPUNCH_STEAMWORKS
        public Steamworks.SteamId id_to_join;
#endif

        public void validate()
        {
            // This user doesn't have a steam account, just use the first 64 bytes of the username hash
            if (user_id == 0)
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var hash = sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(username));
                    byte[] id_bytes = new byte[sizeof(ulong)];
                    for (int i = 0; i < id_bytes.Length; ++i)
                        id_bytes[i] = i < hash.Length ? hash[i] : (byte)0; // Pad with 0 if needed
                    user_id = System.BitConverter.ToUInt64(id_bytes, 0);
                }
            }

            if (username == null)
                throw new System.Exception("Unkown username when joining!");

            if (mode == MODE.LOAD_AND_HOST || mode == MODE.CREATE_AND_HOST)
                if (world_name == null)
                    throw new System.Exception("No world name specified in game startup!");

#if FACEPUNCH_STEAMWORKS
            if (mode == MODE.JOIN_STEAM)
                if (id_to_join == default)
                    throw new System.Exception("No steam id specified when joining!");
#endif
        }

    }
    public static startup_info startup;

    /// <summary> Load/host a game from disk. </summary>
    public static void load_and_host_world(string world_name, string username, ulong user_id)
    {
        startup = new startup_info
        {
            user_id = user_id,
            username = username,
            mode = startup_info.MODE.LOAD_AND_HOST,
            world_name = world_name,
        };

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    /// <summary> Create/host a new world. </summary>
    public static void create_and_host_world(string world_name, int seed, string username, ulong user_id)
    {
        startup = new startup_info
        {
            user_id = user_id,
            username = username,
            mode = startup_info.MODE.CREATE_AND_HOST,
            world_name = world_name,
            world_seed = seed,
        };

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    /// <summary> Join a world hosted on a server. </summary>
    public static bool join_world(string ip_port, string username, ulong user_id)
    {
        int port = server.DEFAULT_PORT;
        string host = ip_port;

        if (ip_port.Contains(":"))
        {
            var split = ip_port.Split(':');
            if (split.Length != 2) return false;
            host = split[0];
            if (!int.TryParse(split[1], out port)) return false;
        }

        startup = new startup_info
        {
            user_id = user_id,
            username = username,
            mode = startup_info.MODE.JOIN,
            hostname = host,
            port = port
        };

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
        return true;
    }

#if FACEPUNCH_STEAMWORKS
    public static bool join_steam_friend(Steamworks.SteamId their_id, string my_username, ulong my_user_id)
    {
        startup = new startup_info
        {
            user_id = my_user_id,
            username = my_username,
            mode = startup_info.MODE.JOIN_STEAM,
            id_to_join = their_id
        };

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
        return true;
    }
#endif

    /// <summary> Returns true if the render range is allowed
    /// to change in the given way. </summary>
    static bool allow_render_range_change(float old_val, float new_val)
    {
        if (old_val == new_val) return false;
        if (new_val < MIN_RENDER_RANGE) return false;
        if (new_val > old_val &&
            chunk.enabled_and_generating >= chunk.generating_limit) return false;
        return true;
    }

    public const float DEFAULT_RENDER_RANGE = 100;

    /// <summary> The target render range, which the actual render range will lerp to. </summary>
    public static float render_range_target
    {
        get { return _render_range_target; }
        set
        {
            if (!allow_render_range_change(_render_range_target, value)) return;
            _render_range_target = value;
        }
    }
    private static float _render_range_target = DEFAULT_RENDER_RANGE;

    /// <summary> How far the player can see. </summary>
    public static float render_range
    {
        get { return _render_range; }
        set
        {
            if (!allow_render_range_change(_render_range, value)) return;
            _render_range = value;
            player.current.update_render_range();
            water_reflections.water_range = value;
        }
    }
    private static float _render_range = DEFAULT_RENDER_RANGE;

    public static void save_and_quit(bool delete_player = false)
    {
        // Disconnect from the network
        client.disconnect(true, delete_player: delete_player);
    }

    //######//
    // INFO //
    //######//

    public string graphics_info()
    {
        return "    FPS             : " + System.Math.Round(1 / Time.deltaTime, 0) +
                                    " (physics: " + System.Math.Round(1 / Time.fixedDeltaTime, 0) +
                                    " target: " + Application.targetFrameRate + ")\n" +
               "    Fullscreen mode : " + Screen.fullScreenMode + "\n" +
               "    Resolution      : " + Screen.width + " x " + Screen.height + "\n" +
               "       Display      : " + Screen.currentResolution.width + " x " + Screen.currentResolution.height;
    }
}
