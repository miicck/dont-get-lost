using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class console : MonoBehaviour
{
    /// <summary> The console in this game session. </summary>
    static console current;

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
        current.process_command(current.last_command);
    }

    /// <summary> The input field where console commands are typed. </summary>
    UnityEngine.UI.InputField input;

    void Start()
    {
        // Starts closed
        input = GetComponent<UnityEngine.UI.InputField>();
        current = this;
        open = false;

        // Called when the player hits enter after typing a command
        input.onEndEdit.AddListener((string command) =>
        {
            if (command != "`") process_command(command);
            open = false;
            input.text = "";
        });
    }

    /// <summary> Throw the given console error message 
    /// to the player. Returns false. </summary>
    bool console_error(string msg)
    {
        popup_message.create(msg);
        return false;
    }

    string last_command = "";

    /// <summary> Process the given console command. </summary>
    bool process_command(string command)
    {
        last_command = command;
        var args = command.Split(null);

        switch (args[0])
        {
            // Give the local player some items e.g [give 100 coin]
            case "give":

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

            // Damage myself e.g [damage 10]
            case "damage":

                if (args.Length < 2) return console_error("Not enough arguments!");

                if (!int.TryParse(args[1], out int damage))
                    return console_error("Could not parse damage from " + args[1]);

                player.current.take_damage(damage);
                return true;

            // Heal myself e.g [heal 10]
            case "heal":

                if (args.Length < 2) return console_error("Not enough arguments!");

                if (!int.TryParse(args[1], out int heal))
                    return console_error("Could not parse heal amount from " + args[1]);

                player.current.heal(heal);
                return true;

            // Teleport to a location
            case "teleport":

                if (args.Length < 4) return console_error("Not enough arguments!");

                if (!float.TryParse(args[1], out float x))
                    return console_error("Could not parse coordinate from " + args[1]);
                if (!float.TryParse(args[2], out float y))
                    return console_error("Could not parse coordinate from " + args[2]);
                if (!float.TryParse(args[3], out float z))
                    return console_error("Could not parse coordinate from " + args[3]);

                player.current.teleport(new Vector3(x, y, z));
                return true;

            // Set the time of day
            case "time":

                if (args.Length < 2) return console_error("Not enough arguments!");
                if (!float.TryParse(args[1], out float t))
                    return console_error("Could not parse time from " + args[1]);
                if (t < 0 || t > 2f)
                    return console_error("Time " + t + " out of range [0,2]!");
                time_manager.time = t;
                return true;

            // Spawn a character
            case "spawn":

                if (args.Length < 2) return console_error("Not enough arguments!");

                string character_to_spawn = "characters/" + args[1];
                count = 1;
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

            // Enter fly (cinematic) mode
            case "fly":
                player.current.fly_mode = !player.current.fly_mode;
                return true;

            // Get information on which meshes are contributing
            // the most verticies to the scene
            case "mesh_info":

                // Report mesh info relative to these objects
                var parent_types = new List<System.Type>
                {
                    typeof(world_object),
                    typeof(character),
                    typeof(item),
                    typeof(random_object),
                    typeof(random_ore)
                };

                Dictionary<string, mesh_info> results = new Dictionary<string, mesh_info>();
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
                        var inf = results[name];
                        inf.total_instances += 1;
                        inf.total_verticies += r.mesh.vertexCount;
                        results[name] = inf;
                    }
                    else
                        results[name] = new mesh_info
                        {
                            total_verticies = r.mesh.vertexCount,
                            total_instances = 1
                        };
                }

                List<KeyValuePair<string, mesh_info>> list = new List<KeyValuePair<string, mesh_info>>(results);
                list.Sort((a, b) => a.Value.total_verticies < b.Value.total_verticies ? 1 : -1);

                string to_print = "";
                foreach (var kv in list)
                    to_print += kv.Key + " : " + kv.Value + "\n";

                Debug.Log(to_print);
                return true;

            // Increase, or decrease hunger
            case "hunger":

                if (args.Length < 2) return console_error("Too few arguments specified!");
                if (!int.TryParse(args[1], out int hunger))
                    return console_error("Could not parse an integer from the arguement " + args[1]);

                player.current.modify_hunger(hunger);
                return true;

            // Enable, or disable characters
            case "characters":
                character.characters_enabled = !character.characters_enabled;
                return true;

            default:
                return console_error("Unkown command " + args[0]);
        }
    }

    struct mesh_info
    {
        public int total_verticies;
        public int total_instances;

        public override string ToString()
        {
            return " Verticies " + total_verticies + " Instances " + total_instances;
        }
    }
}