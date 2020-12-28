using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class console : MonoBehaviour
{
    /// <summary> The console in this game session. </summary>
    static console current;
    static List<string> command_history;
    static int command_history_position = 0;

    public delegate bool console_command(string[] args);
    public struct console_info
    {
        public console_command command;
        public string description;
        public string usage_example;
    };

    public static Dictionary<string, console_info> commands = new Dictionary<string, console_info>
    {
        ["give"] = new console_info
        {
            command = (args) =>
            {
                item item = null;
                if (args.Length < 2) return console_error("Not enough arguments!");

                if (args.Length < 3)
                {
                    // If only two arguments given, assume count = 1
                    item = Resources.Load<item>("items/" + args[1]);
                    if (item == null)
                        return console_error("Could not identify item " + args[1]);
                    player.current.inventory.add(item, 1);
                    return true;
                }

                if (!int.TryParse(args[1], out int count))
                    return console_error("Could not parse quantity from " + args[1]);

                item = Resources.Load<item>("items/" + args[2]);
                if (item == null)
                    return console_error("Could not identify item " + args[2]);

                player.current.inventory.add(item, count);
                return true;
            },

            description = "Adds items to the current player's inventory.",
            usage_example = "give log\ngive 100 log"
        },

        ["fly"] = new console_info
        {
            command = (args) =>
            {
                player.current.fly_mode = !player.current.fly_mode;
                return true;
            },

            description = "Toggle fly mode. In fly mode left/right click can be used to add/remove " +
                          "cinematic keyframes, which can be played back by pressing p.",
            usage_example = "fly"
        },

        ["time"] = new console_info
        {
            command = (args) =>
            {
                if (args.Length < 2) return console_error("Not enough arguments!");
                if (!float.TryParse(args[1], out float t))
                    return console_error("Could not parse time from " + args[1]);
                if (t < 0 || t > 2f)
                    return console_error("Time " + t + " out of range [0,2]!");
                time_manager.time = t;
                return true;
            },

            description = "Sets the current time of day. The time is in the range [0, 2], where 0 is the" +
                          " start of the day and 1 is the start of the night.",
            usage_example = "time 0"
        },

        ["hunger"] = new console_info
        {
            command = (args) =>
            {
                if (args.Length < 2) return console_error("Too few arguments specified!");
                if (!int.TryParse(args[1], out int hunger))
                    return console_error("Could not parse an integer from the arguement " + args[1]);

                player.current.modify_hunger(hunger);
                return true;
            },

            description = "Modifies the current players hunger by the amount" +
                          " given (negative values make the player more hungry).",
            usage_example = "hunger 100\nhunger -10"
        },

        ["kill_all"] = new console_info
        {
            command = (args) =>
            {
                foreach (var c in FindObjectsOfType<character>())
                    if (!c.is_client_side)
                        c.delete();
                return true;
            },

            description = "Kills all characters (NPCs).",
            usage_example = "kill_all"
        },

        ["delete_player"] = new console_info
        {
            command = (args) =>
            {
                game.save_and_quit(delete_player: true);
                return true;
            },

            description = "Logs out the current player, but also removes them from the server entirely.",
            usage_example = "delete_player"
        },

        ["damage"] = new console_info
        {
            command = (args) =>
            {
                if (args.Length < 2) return console_error("Not enough arguments!");

                if (!int.TryParse(args[1], out int damage))
                    return console_error("Could not parse damage from " + args[1]);

                player.current.take_damage(damage);
                return true;
            },

            description = "Damages the current player by the amount given (the player has 100 health).",
            usage_example = "damage 99"
        },

        ["heal"] = new console_info
        {
            command = (args) =>
            {
                if (args.Length < 2) return console_error("Not enough arguments!");

                if (!int.TryParse(args[1], out int heal))
                    return console_error("Could not parse heal amount from " + args[1]);

                player.current.heal(heal);
                return true;
            },

            description = "Heals the current player by the amount given (the player has 100 health).",
            usage_example = "heal 99"
        },

        ["teleport"] = new console_info
        {
            command = (args) =>
            {
                if (player.current == null) return console_error("Please wait until the local player is loaded");

                if (args.Length == 2)
                {
                    // Teleport to player
                    var pi = client.get_player_info(args[1]);
                    if (pi == null || !pi.connected) return console_error("Player " + args[1] + " is not connected!");
                    player.current.teleport(pi.position);
                    return true;
                }

                if (args.Length < 4) return console_error("Not enough arguments!");

                // Teleport to location
                if (!float.TryParse(args[1], out float x))
                    return console_error("Could not parse coordinate from " + args[1]);
                if (!float.TryParse(args[2], out float y))
                    return console_error("Could not parse coordinate from " + args[2]);
                if (!float.TryParse(args[3], out float z))
                    return console_error("Could not parse coordinate from " + args[3]);

                player.current.teleport(new Vector3(x, y, z));
                return true;
            },

            description = "Teleports the player to the given x, y, z coordinates.",
            usage_example = "teleport 1000 32 -1000"
        },

        ["spawn"] = new console_info
        {
            command = (args) =>
            {
                if (args.Length < 2) return console_error("Not enough arguments!");

                string character_to_spawn = "characters/" + args[1];
                int count = 1;
                if (args.Length > 2)
                {
                    character_to_spawn = "characters/" + args[2];
                    if (!int.TryParse(args[1], out count))
                        return console_error("Could not parse count from " + args[1]);
                }

                if (Resources.Load<character>(character_to_spawn) == null)
                    return console_error("Unkown character: " + args[1]);

                if (player.current == null) return true;

                var ray = player.current.camera_ray();
                if (Physics.Raycast(ray, out RaycastHit hit))
                    for (int i = 0; i < count; ++i)
                        client.create(hit.point, character_to_spawn);

                return true;
            },

            description = "Spawn a given character where the player is looking",
            usage_example = "spawn chicken\nspawn 10 chicken"
        },

        ["mesh_info"] = new console_info
        {
            command = (args) =>
            {
                var parent_types = new List<System.Type>
                {
                    typeof(world_object),
                    typeof(character),
                    typeof(item),
                    typeof(random_object),
                    typeof(random_ore)
                };

                Dictionary<string, KeyValuePair<int, int>> results =
                    new Dictionary<string, KeyValuePair<int, int>>();
                foreach (var r in FindObjectsOfType<MeshFilter>())
                {
                    string name = r.name;
                    foreach (var pt in parent_types)
                    {
                        var found = r.GetComponentInParent(pt);
                        if (found == null) continue;
                        name = found.name + " (" + pt.Name + ")";
                        break;
                    }

                    if (results.ContainsKey(name))
                    {
                        var found = results[name];
                        results[name] = new KeyValuePair<int, int>(
                            found.Key + 1, found.Value + r.mesh.vertexCount
                        );
                    }
                    else
                        results[name] = new KeyValuePair<int, int>(
                           1, r.mesh.vertexCount);
                }

                List<KeyValuePair<string, KeyValuePair<int, int>>> list = new List<KeyValuePair<string, KeyValuePair<int, int>>>(results);
                list.Sort((a, b) => b.Value.Value.CompareTo(a.Value.Value));

                string to_print = "";
                foreach (var kv in list)
                    to_print += kv.Key + " : " + kv.Value.Key + " instances " + kv.Value.Value + " total verticies\n";

                Debug.Log(to_print);
                return true;
            },

            description = "Reports information about which meshes are contributing the most verticies to the scene.",
            usage_example = "mesh_info",
        },

        ["characters"] = new console_info
        {
            command = (args) =>
            {
                character.characters_enabled = !character.characters_enabled;
                popup_message.create("Characters " + (character.characters_enabled ? "enabled" : "disabled"));
                return true;
            },

            description = "Toggles the spawning of characters.",
            usage_example = "characters"
        },

        ["refresh_connections"] = new console_info
        {
            command = (args) =>
            {
                item_node.refresh_connections();
                return true;
            },

            description = "Refresh the connections in the item transport system.",
            usage_example = "refresh_connections"
        },

        ["toggle_overlay"] = new console_info
        {
            command = (args) =>
            {
                item_node.display_enabled = !item_node.display_enabled;
                settler_path_element.draw_links = item_node.display_enabled;
                return true;
            },

            description = "Toggles the display of overlays in the game world " +
                          "(e.g. information about the transport system)",
            usage_example = "toggle_overlay"
        },

        ["cursor"] = new console_info
        {
            command = (args) =>
            {
                if (args.Length < 2) return console_error("Not enough arguments!");
                var sprite = Resources.Load<Sprite>("sprites/" + args[1]);
                if (sprite == null) return console_error("Could not find the sprite: " + args[1]);
                player.current.cursor_sprite = args[1];
                return true;
            },

            description = "Set the current cursor",
            usage_example = "cursor transparent"
        },

        ["create"] = new console_info
        {
            command = (args) =>
            {
                if (args.Length < 2) return console_error("Not enough arguments!");
                if (player.current == null) return console_error("Please wait for the player to load!");
                if (Resources.Load<networked>(args[1]) == null) return console_error("Could not find the networked prefab: " + args[1]);
                if (Physics.Raycast(player.current.camera_ray(), out RaycastHit hit))
                    client.create(hit.point, args[1]);
                return true;
            },

            description = "Create a networked prefab with the given resource path where the player is looking",
            usage_example = "create characters/chicken"
        },

        ["delete_type"] = new console_info
        {
            command = (args) =>
            {
                if (args.Length < 2) return console_error("Not enough arguments!");
                var t = System.Type.GetType(args[1]);
                if (t == null) return console_error("Could not find the type: " + args[1]);
                if (!t.IsSubclassOf(typeof(networked))) return console_error(t + " is not a networked type!");

                var found = (networked)FindObjectOfType(t);
                found.delete();
                return true;
            },

            description = "Deletes the first found instance of the given network type.",
            usage_example = "Delete character"
        },

        ["trigger_attack"] = new console_info
        {
            command = (args) =>
            {
                player.call_when_current_player_available(() =>
                {
                    var gates = FindObjectsOfType<town_gate>();
                    var gate = utils.find_to_min(gates, (g) =>
                        (player.current.transform.position - g.transform.position).magnitude);
                    if (gate == null)
                    {
                        console_error("Could not find a town gate to attack!");
                        return;
                    }
                    gate.trigger_attack(args);
                });
                return true;
            },

            description = "Trigger an attack on the nearest town gate to the player.",
            usage_example = "trigger_attack"
        }
    };

    /// <summary> True if the console window is open/selected. </summary>
    public static bool open
    {
        get
        {
            if (current == null) return false;
            return current.gameObject.activeInHierarchy;
        }
        set
        {
            if (current == null) return;
            current.gameObject.SetActive(value);
            if (value)
                current.input.ActivateInputField();
        }
    }

    public static void repeat_last_command()
    {
        if (current == null) return;
        if (command_history.Count == 0) return;
        current.process_command(command_history[command_history.Count - 1], record: false);
    }

    /// <summary> The input field where console commands are typed. </summary>
    UnityEngine.UI.InputField input;

    void Start()
    {
        // Starts closed
        input = GetComponent<UnityEngine.UI.InputField>();
        current = this;
        open = false;
        command_history = new List<string>();

        // Called when the player hits enter after typing a command
        input.onEndEdit.AddListener((string command) =>
        {
            if (command != "`") process_command(command);
            open = false;
            input.text = "";
        });
    }

    bool in_history_range(int i)
    {
        if (i < 0) return false;
        if (i >= command_history.Count) return false;
        return true;
    }

    private void Update()
    {
        bool fill_from_history = false;
        if (controls.key_press(controls.BIND.CONSOLE_MOVE_HISTORY_BACK))
        {
            if (in_history_range(command_history_position - 1))
                command_history_position -= 1;
            fill_from_history = true;
        }
        else if (controls.key_press(controls.BIND.CONSOLE_MOVE_HISTORY_FORWARD))
        {
            if (in_history_range(command_history_position + 1))
                command_history_position += 1;
            fill_from_history = true;
        }

        if (fill_from_history && in_history_range(command_history_position))
            input.text = command_history[command_history_position];
    }

    /// <summary> Throw the given console error message 
    /// to the player. Returns false. </summary>
    static bool console_error(string msg)
    {
        popup_message.create(msg);
        return false;
    }

    /// <summary> Process the given console command. </summary>
    bool process_command(string command, bool record = true)
    {
        if (record) command_history.Add(command);
        command_history_position = command_history.Count;
        var args = command.Split(null);

        if (!commands.TryGetValue(args[0], out console_info info))
            return console_error("Unkown command " + args[0]);

        return info.command(args);
    }
}