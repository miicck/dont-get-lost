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
    public Text message;

    /// <summary> The message to display when the world menu starts. </summary>
    public static string message_to_display = "";

    UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData hd_camera;

    string get_username()
    {
        string username = username_input.text.Trim();
        if (username.Length == 0)
        {
            error_highlighter.highlight(username_input.GetComponent<Image>());
            return null;
        }
        return username;
    }

    ulong user_id => steam.connected ? steam.steam_id : 0;

    public delegate void NewWorldCreator(bool enable_tutorial);

    void setup_buttons()
    {
        // Ensure the mouse is visible
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Display the message, but reset it for next time
        message.text = message_to_display;
        message_to_display = "";

        // Find saved worlds (defaulting to steam saves)
        var world_files = server.existing_steam_saves();
        var is_steam_world = new List<bool>();
        foreach (var wf in world_files)
            is_steam_world.Add(true);

        foreach (var lf in server.existing_local_saves())
            if (!world_files.Contains(System.IO.Path.GetFileName(lf)))
            {
                world_files.Add(lf);
                is_steam_world.Add(false);
            }

        var load_header = template_header.inst(button_container);
        load_header.text = "Load existing world";
        if (world_files.Count == 0) load_header.text = "No existing worlds";

        int world_count = world_files.Count;

        // Initialize username input field
        if (steam.connected)
        {
            username_input.interactable = false;
            username_input.text = steam.username();
        }
        else
        {
            username_input.text = PlayerPrefs.GetString("username");
        }

        for (int i = 0; i < world_files.Count; ++i)
        {
            var wf = world_files[i];
            bool is_steam = is_steam_world[i];

            var button = template_world_button.inst(button_container);
            string name = System.IO.Path.GetFileNameWithoutExtension(wf);

            button.Find("disk").gameObject.SetActive(!is_steam);
            button.Find("cloud").gameObject.SetActive(is_steam);

            var load_button = button.Find("load").GetComponent<Button>();
            load_button.GetComponentInChildren<Text>().text = name;
            load_button.onClick.AddListener(() =>
            {
                string username = get_username();
                if (username == null) return;

                PlayerPrefs.SetString("username", username);
                game.load_and_host_world(name, username, user_id);
            });

            var delete_button = button.Find("delete").GetComponent<Button>();
            delete_button.onClick.AddListener(() =>
            {
                Destroy(button.gameObject);
                if (is_steam)
                    steam.delete_file(System.IO.Path.GetFileName(wf));
                else
                    System.IO.File.Delete(wf);
                Debug.Log(wf);
                world_count -= 1;
                if (world_count == 0)
                    load_header.text = "No existing worlds";
            });
        }

        var create_header = template_header.inst(button_container);
        create_header.text = "Create new world";

        // Move world name/seed inputs to bottom
        world_name_input.transform.SetAsLastSibling();
        world_seed_input.transform.SetAsLastSibling();

        NewWorldCreator create_new_world = (enable_tutorial) =>
        {
            string username = get_username();
            if (username == null)
                return;

            string name = world_name_input.text.Trim();
            if (name.Length == 0 || server.save_exists(name))
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

            game.create_and_host_world(name, seed, username, user_id, !enable_tutorial);
        };

        // Create new world button
        var new_world = template_button.inst(button_container);
        new_world.GetComponentInChildren<Text>().text = "New world";
        new_world.onClick.AddListener(() => create_new_world(true));

        var new_world_no_tutorial = template_button.inst(button_container);
        new_world_no_tutorial.GetComponentInChildren<Text>().text = "New world (no tutorial)";
        new_world_no_tutorial.onClick.AddListener(() => create_new_world(false));

        var join_header = template_header.inst(button_container);
        join_header.text = "Join game over network";

        var join_button = template_button.inst(button_container);
        join_button.GetComponentInChildren<Text>().text = "Join";
        join_button.onClick.AddListener(() =>
        {
            string username = username_input.text.Trim();
            if (username.Length == 0)
                error_highlighter.highlight(username_input.GetComponent<Image>());
            else
            {
                PlayerPrefs.SetString("last_ip_joined", ip_input.text);
                if (!game.join_world(ip_input.text, username, user_id))
                    error_highlighter.highlight(ip_input.GetComponentInChildren<Image>());
            }
        });

        ip_input.text = PlayerPrefs.GetString("last_ip_joined",
            network_utils.local_ip_address() + ":" + server.DEFAULT_PORT);
        ip_input.transform.SetAsLastSibling();

        update_steam_friends();

        var quit_header = template_header.inst(button_container);
        quit_header.text = "";

        var quit_button = template_button.inst(button_container);
        quit_button.GetComponentInChildren<Text>().text = "Quit game";
        quit_button.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        // Remove the templates
        template_button.gameObject.SetActive(false);
        template_header.gameObject.SetActive(false);
        template_world_button.gameObject.SetActive(false);

        InvokeRepeating("update_steam_friends", 1, 1);
    }

    private void Start()
    {
        hd_camera = menu_camera.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        setup_buttons();
    }

    Text steam_friends_header;
    Dictionary<ulong, client.join_query> joinable_queries = new Dictionary<ulong, client.join_query>();
    Dictionary<ulong, string> joinable_queries_unames = new Dictionary<ulong, string>();

    void update_steam_friends()
    {
#if FACEPUNCH_STEAMWORKS

        int friends_count = 0;

        if (steam_friends_header == null)
        {
            steam_friends_header = template_header.inst(button_container);
            steam_friends_header.text = "Join steam friends";
        }

        // Delete old steam information
        foreach (Transform t in button_container.transform)
            if (t.name.StartsWith("steam"))
                Destroy(t.gameObject);

        if (steam.connected)
        {
            HashSet<ulong> online_ids = new HashSet<ulong>();
            foreach (var f in Steamworks.SteamFriends.GetFriends())
                if (f.IsPlayingThisGame)
                {
                    online_ids.Add(f.Id);

                    if (joinable_queries.TryGetValue(f.Id, out client.join_query query))
                        if (query.query_complete)
                        {
                            // This steam friend was found to be joinable
                            if (query.result)
                                continue;

                            // This steam friend was not found to be joinable, we'll ask again
                            query.close();
                            joinable_queries.Remove(f.Id);
                        }

                    if (!joinable_queries.ContainsKey(f.Id))
                    {
                        // Start a new joinable query
                        joinable_queries[f.Id] = new client.join_query(
                            new steamworks_client_backend(f.Id,
                            steamworks_server_backend.CLIENT_TO_SERVER_CHANNEL));

                        joinable_queries_unames[f.Id] = f.Name;
                    }
                }

            // Remove queries to friends that logged out
            List<ulong> to_remove = new List<ulong>();
            foreach (var kv in joinable_queries)
                if (!online_ids.Contains(kv.Key))
                    to_remove.Add(kv.Key);
            foreach (var id in to_remove)
            {
                joinable_queries[id].close();
                joinable_queries.Remove(id);
            }

            foreach (var kv in joinable_queries)
            {
                var id = kv.Key;
                var query = kv.Value;

                ++friends_count;
                var join_friend_button = template_button.inst(button_container);
                join_friend_button.gameObject.SetActive(true);
                join_friend_button.name = "steam_friend_" + id;
                join_friend_button.GetComponentInChildren<Text>().text =
                    joinable_queries_unames[id] + " " + (query.result ? "(joinable)" : "(not joinable)");

                if (!query.result)
                    join_friend_button.colors = ui_colors.greyed_out_color_block(fade_duration: 0);

                join_friend_button.transform.SetSiblingIndex(steam_friends_header.transform.GetSiblingIndex() + 1);

                join_friend_button.onClick.AddListener(() =>
                {
                    var username = get_username();
                    if (username == null) return;
                    game.join_steam_friend(id, username, user_id);
                });
            }

            if (friends_count == 0) // :'(
            {
                var no_friends_header = template_header.inst(button_container);
                no_friends_header.gameObject.SetActive(true);
                no_friends_header.name = "steam_no_friends";
                no_friends_header.transform.SetSiblingIndex(steam_friends_header.transform.GetSiblingIndex() + 1);
                no_friends_header.text = "No steam friends in-game";
            }
        }
        else
        {
            var not_connected_header = template_header.inst(button_container);
            not_connected_header.gameObject.SetActive(true);
            not_connected_header.name = "steam_not_connected";
            not_connected_header.transform.SetSiblingIndex(steam_friends_header.transform.GetSiblingIndex() + 1);
            not_connected_header.text = "Not connected to steam";
        }

#endif // FACEPUNCH_STEAMWORKS
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
        foreach (var kv in joinable_queries)
            kv.Value.update();

        Color bg = hd_camera.backgroundColorHDR;
        float h, s, v;
        Color.RGBToHSV(bg, out h, out s, out v);
        h += Time.deltaTime / 100f;
        while (h > 1.0f) h -= 1.0f;
        hd_camera.backgroundColorHDR = Color.HSVToRGB(h, s, v);
    }
}
