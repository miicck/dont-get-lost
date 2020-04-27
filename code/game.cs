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
        networked.server.start(networked.server.DEFAULT_PORT);
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

    public static bool join_world(string ip_port)
    {
        string ip = ip_port.Split(':')[0];
        string port_str = ip_port.Split(':')[1];
        int port;
        if (!int.TryParse(port_str, out port))
            return false;

        networked.client.connect_to_server(ip, port);

        // Create the world object
        DontDestroyOnLoad(networked.section.create<world>(0, "unloaded!"));
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
        return true;
    }

    public static void create_and_host_world(string name, int seed)
    {
        // Start the server
        networked.server.start(networked.server.DEFAULT_PORT);
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
        // Move the world (which was saved from the world_menu scene)
        // to the current scene so it gets destroyed when the main scene does.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
            FindObjectOfType<world>().gameObject,
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

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
            debug_text.text += "Server info:\n";
            debug_text.text += networked.server.info() + "\n";
        }

        if (networked.client.connected)
        {
            networked.client.update();
            debug_text.text += "Client info:\n";
            debug_text.text += networked.client.info() + "\n";
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
