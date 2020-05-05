using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class game : MonoBehaviour
{
    public const float MIN_RENDER_RANGE = 0f;

    public UnityEngine.UI.Text debug_text;

    static string world_loading;
    public static void load_and_host_world(string world_name, string username)
    {
        world_loading = world_name;
        player.username = username;
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    static bool creating_world = false;
    static int seed_creating = 0;
    public static void create_and_host_world(string world_name, int seed, string username)
    {
        world_loading = world_name;
        seed_creating = seed;
        player.username = username;
        creating_world = true;
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    static string ip_connecting = null;
    static int port_connecting = 0;
    public static bool join_world(string ip_port, string username)
    {
        player.username = username;
        ip_connecting = ip_port.Split(':')[0];
        string port_str = ip_port.Split(':')[1];
        if (!int.TryParse(port_str, out port_connecting))
            return false;

        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
        return true;
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
            player.current.update_render_range();
        }
    }

    void Start()
    {
        if (ip_connecting == null)
        {
            // Not connecting, start a local server+client
            server.start(server.DEFAULT_PORT, world_loading,
                "misc/player_local", "misc/player_remote");

            client.connect(network_utils.local_ip_address().ToString(),
                server.DEFAULT_PORT, player.username, "password");

            // Create the world object
            if (creating_world)
            {
                client.create(Vector3.zero, "misc/world");
                world.seed = seed_creating;
            }
        }
        else
        {
            client.connect(ip_connecting, port_connecting, player.username, "password");
        }

        // Start with invisible, locked cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

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
        debug_text.text = "";

        debug_text.text += "World: " + world.name + " (seed " + world.seed + ")\n";
        debug_text.text += "FPS: " + System.Math.Round(1 / Time.deltaTime, 0) + "\n";

        server.update();
        debug_text.text += server.info() + "\n";

        client.update();
        debug_text.text += client.info() + "\n";

        if (player.current != null)
        {
            debug_text.text += "\nPlayer info:\n";
            debug_text.text += player.current.info();
        }

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
