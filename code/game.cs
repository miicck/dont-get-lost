using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class game : MonoBehaviour
{
    public static player player { get; private set; }

    public bool regenerate = false;

    // How far the player can see
    private static float _render_range = 100f;
    private static float render_range_target = 100f;
    public static float render_range
    {
        get { return _render_range; }
        private set
        {
            if (value < chunk.SIZE) value = chunk.SIZE;
            _render_range = value;
            player.update_render_range();
        }
    }

    void Start()
    {
        // Delete the world if we're running from the editor
        // so we regenerate every time
        if (regenerate)
            System.IO.Directory.Delete(world.save_folder(), true);

        // Create the ui
        canvas.create();

        // Create the player
        player = player.create();

        // Create the sun
        var sun = new GameObject("sun").AddComponent<Light>();
        sun.transform.position = Vector3.zero;
        sun.transform.LookAt(new Vector3(1, -2, 1));
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        RenderSettings.ambientSkyColor = new Color(0.3f, 0.3f, 0.3f);
    }

    void Update()
    {
        world.update_grid(player.transform.position);

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

        // Update the ui
        canvas.update();
    }

    void OnApplicationQuit()
    {
        // Save the world
        world.destroy();
    }

    void OnDrawGizmos()
    {
        if (player != null)
            world.draw_gizmos(player.transform.position);
    }
}
