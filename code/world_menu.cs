using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Security.Cryptography;

// Used to highlight an incorrect input
public class error_highlighter : MonoBehaviour
{
    public static void highlight(Image image, float fade_duration = 1.0f)
    {
        var existing = image.gameObject.GetComponent<error_highlighter>();
        if (existing != null)
        {
            image.color = existing.reset_color;
            Destroy(existing);
        }

        var eh = image.gameObject.AddComponent<error_highlighter>();
        eh.initial_color = Color.red;
        eh.reset_color = image.color;
        eh.start_time = Time.realtimeSinceStartup;
        eh.image = image;
        eh.duration = fade_duration;
    }

    Color initial_color;
    Color reset_color;
    float start_time;
    float duration;
    Image image;

    private void Update()
    {
        float t = Time.realtimeSinceStartup - start_time;
        t /= duration;

        image.color = Color.Lerp(initial_color, reset_color, t);
        if (t >= 1.0f)
        {
            image.color = reset_color;
            Destroy(this);
        }
    }
}

public class world_menu : MonoBehaviour
{
    public Button template_button;
    public Transform template_world_button;
    public Text template_header;
    public Transform button_container;
    public Camera menu_camera;
    public InputField world_name_input;
    public InputField world_seed_input;
    public InputField ip_input;
    public InputField username_input;

    private void Start()
    {
        // Find all the saved worlds
        var world_folders = System.IO.Directory.GetDirectories(networked.server.saves_directory());

        var load_header = template_header.inst();
        load_header.text = "Load existing world...";
        if (world_folders.Length == 0) load_header.text = "No existing worlds";
        load_header.transform.SetParent(button_container);

        int world_count = world_folders.Length;

        foreach (var wf in world_folders)
        {
            var button = template_world_button.inst();
            button.transform.SetParent(button_container);

            string[] splt = wf.Split('/', '\\');
            string name = splt[splt.Length - 1];

            var load_button = button.Find("load").GetComponent<Button>();
            load_button.GetComponentInChildren<Text>().text = name;
            load_button.onClick.AddListener(() =>
            {
                string username = username_input.text.Trim();
                if (username.Length == 0)
                    error_highlighter.highlight(username_input.GetComponent<Image>());
                else game.load_and_host_world(name, username);
            });

            var delete_button = button.Find("delete").GetComponent<Button>();
            delete_button.onClick.AddListener(() =>
            {
                Destroy(button.gameObject);
                System.IO.Directory.Delete(wf, true);
                world_count -= 1;
                if (world_count == 0)
                    load_header.text = "No existing worlds";
            });
        }

        var create_header = template_header.inst();
        create_header.text = "Create new world...";
        create_header.transform.SetParent(button_container);

        // Move world name/seed inputs to bottom
        world_name_input.transform.SetAsLastSibling();
        world_seed_input.transform.SetAsLastSibling();

        // Create new world button
        var new_world = template_button.inst();
        new_world.transform.SetParent(button_container);
        new_world.GetComponentInChildren<Text>().text = "New world";
        new_world.onClick.AddListener(() =>
        {
            string username = username_input.text.Trim();
            if (username.Length == 0)
            {
                error_highlighter.highlight(username_input.GetComponent<Image>());
                return;
            }

            string name = world_name_input.text.Trim();
            string folder = networked.server.saves_directory() + "/" + name;
            if (System.IO.Directory.Exists(folder))
            {
                error_highlighter.highlight(world_name_input.GetComponent<Image>());
                return;
            }

            // Random seed if no seed specified
            // or directly parse seed from input
            // or hash seed from input
            int seed = Random.Range(1, int.MaxValue);
            string ws = world_seed_input.text.Trim();
            if (ws.Length > 0)
            {
                if (!int.TryParse(ws, out seed) || seed == 0)
                    seed = get_hash(ws);
            }

            game.create_and_host_world(name, seed, username);
        });

        var join_header = template_header.inst();
        join_header.text = "Join game over network...";
        join_header.transform.SetParent(button_container);

        var join_button = template_button.inst();
        join_button.transform.SetParent(button_container);
        join_button.GetComponentInChildren<Text>().text = "Join";
        join_button.onClick.AddListener(() =>
        {
            string username = username_input.text.Trim();
            if (username.Length == 0)
                error_highlighter.highlight(username_input.GetComponent<Image>());
            else
                game.join_world(ip_input.text, username);
        });

        ip_input.text = networked.server.local_ip_address() + ":" + networked.server.DEFAULT_PORT;
        ip_input.transform.SetAsLastSibling();

        // Remove the templates
        Destroy(template_button.gameObject);
        Destroy(template_header.gameObject);
        Destroy(template_world_button.gameObject);
    }

    public static int get_hash(string s)
    {
        byte[] hashed;
        using (HashAlgorithm algorithm = SHA256.Create())
            hashed = algorithm.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
        return System.BitConverter.ToInt32(hashed, 0);
    }

    private void Update()
    {
        Color bg = menu_camera.backgroundColor;
        float h, s, v;
        Color.RGBToHSV(bg, out h, out s, out v);
        h += Time.deltaTime / 100f;
        while (h > 1.0f) h -= 1.0f;
        menu_camera.backgroundColor = Color.HSVToRGB(h, s, v);
    }
}
