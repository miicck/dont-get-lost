﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class game : MonoBehaviour
{
    public const float MIN_RENDER_RANGE = 0f;
    public const string LOCAL_PLAYER_PREFAB = "misc/player_local";
    public const string REMOTE_PLAYER_PREFAB = "misc/player_remote";

    public UnityEngine.UI.Text debug_text;

    /// <summary> Information on how to start a game. </summary>
    struct startup_info
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
    static startup_info startup;

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
        }
    }
    private static float _render_range = chunk.SIZE;

    public static void on_local_player_create(player p)
    {
        p.username.value = startup.username;
    }

    void Start()
    {
        // Various startup modes
        switch(startup.mode)
        {
            case startup_info.MODE.LOAD_AND_HOST:
            case startup_info.MODE.CREATE_AND_HOST:

                // Start + join the server
                server.start(server.DEFAULT_PORT, startup.world_name, 
                    LOCAL_PLAYER_PREFAB, REMOTE_PLAYER_PREFAB);

                client.connect(network_utils.local_ip_address().ToString(),
                    server.DEFAULT_PORT, startup.username, "password");

                // Create the world (if required)
                if (startup.mode == startup_info.MODE.CREATE_AND_HOST)
                {
                    var w = (world)client.create(Vector3.zero, "misc/world");
                    w.networked_seed.value = startup.world_seed;
                    w.networked_name.value = startup.world_name;
                }

                break;

            case startup_info.MODE.JOIN:

                // Join the server
                client.connect(startup.hostname, startup.port, 
                    startup.username, "password");

                break;

            default:
                throw new System.Exception("Unkown startup mode!");
        }

        // Start with invisible, locked cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
            debug_text.enabled = !debug_text.enabled;

        debug_text.text = "";

        debug_text.text += world.info() + "\n";
        debug_text.text += "FPS: " + System.Math.Round(1 / Time.deltaTime, 0) + "\n";

        server.update();
        debug_text.text += server.info() + "\n";

        client.update();
        debug_text.text += client.info() + "\n";

        debug_text.text += player.info() + "\n";

        if (Input.GetKeyDown(KeyCode.Equals)) render_range_target += 10f;
        if (Input.GetKeyDown(KeyCode.Minus)) render_range_target -= 10f;
        render_range = Mathf.Lerp(render_range, render_range_target, 3 * Time.deltaTime);
    }

    void OnApplicationQuit()
    {
        // Disconnect from the network
        client.disconnect();
        server.stop();
    }
}
