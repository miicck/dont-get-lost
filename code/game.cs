using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class game : MonoBehaviour
{
    public const float MIN_RENDER_RANGE = 0f;

    public static player player { get; private set; }

    public UnityEngine.UI.Text debug_text;

    public static void load_and_host_world(string world_name, string username)
    {
        world.name = world_name;
        player.username = username;
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    public static void create_and_host_world(string world_name, int seed, string username)
    {
        world.name = world_name;
        world.seed = seed;
        player.username = username;
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
            player.update_render_range();
        }
    }

    void Start()
    {
        if (ip_connecting == null)
        {
            // Not connecting, start a local server+client
            networked.server.set_forced<player>();
            networked.server.start(networked.server.DEFAULT_PORT, world.name);
            networked.client.connect_to_server(
                networked.server.local_ip_address().ToString(),
                networked.server.port);
        }
        else
        {
            networked.client.connect_to_server(
                ip_connecting,
                port_connecting
            );
        }

        // Create the world object (seed + name will be loaded by the server)
        networked.section.create<world>();

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

        if (networked.server.started)
        {
            networked.server.update();
            debug_text.text += "\nServer info:\n";
            debug_text.text += networked.server.info() + "\n";
        }

        if (networked.client.connected)
        {
            networked.client.update();
            debug_text.text += "\nClient info:\n";
            debug_text.text += networked.client.info() + "\n";
        }

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
        if (networked.client.connected) networked.client.disconnect();
        if (networked.server.started) networked.server.stop();
    }
}
