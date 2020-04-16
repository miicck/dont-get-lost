using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class game : MonoBehaviour
{
    public static player player { get; private set; }

    public static void load_world(string path)
    {
        world.seed = int.Parse(System.IO.File.ReadAllText(path + "/seed"));
        world.name = path.Split('/')[path.Split('/').Length - 1];
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    public static void create_world(string name, int seed)
    {
        world.seed = seed;
        world.name = name;
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("scenes/main");
    }

    // The target render range, which the actual render range will lerp to
    private static float _render_range_target = chunk.SIZE;
    public static float render_range_target
    {
        get { return _render_range_target; }
        set
        {
            if (value < chunk.SIZE) value = chunk.SIZE;
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
            if (value < chunk.SIZE) value = chunk.SIZE;
            _render_range = value;
            player.update_render_range();
        }
    }

    void Start()
    {
        // Create the player
        player = player.create();

        // Create the biome at the players location
        var biome_coords = biome.coords(player.transform.position);
        biome.generate(biome_coords[0], biome_coords[1]);

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
        if (Input.GetKeyDown(KeyCode.Equals))
            render_range_target += 10f;
        if (Input.GetKeyDown(KeyCode.Minus))
            render_range_target -= 10f;

        render_range = Mathf.Lerp(render_range, render_range_target, 3 * Time.deltaTime);

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

    void OnApplicationQuit()
    {
        // Save the player
        player.current.save();

        // Save the world seed
        System.IO.File.WriteAllText(world.save_folder() + "/seed", "" + world.seed);
    }
}
