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
        get => current.gameObject.activeInHierarchy;
        set
        {
            current.gameObject.SetActive(value);
            if (value)
                current.input.ActivateInputField();
        }
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

    /// <summary> Process the given console command. </summary>
    bool process_command(string command)
    {
        var args = command.Split(null);

        switch (args[0])
        {
            // Give the local player some items e.g [give 100 coin]
            case "give":

                if (args.Length < 2) return console_error("Not enough arguments!");

                if (args.Length < 3)
                {
                    // If only two arguments given, assume count = 1
                    if (Resources.Load<item>("items/" + args[1]) == null)
                        return console_error("Could not identify item " + args[1]);
                    player.current.inventory.add(args[1], 1);
                    return true;
                }

                if (!int.TryParse(args[1], out int count))
                    return console_error("Could not parse quantity from " + args[1]);

                if (Resources.Load<item>("items/" + args[2]) == null)
                    return console_error("Could not identify item " + args[2]);

                player.current.inventory.add(args[2], count);
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

            default:
                return console_error("Unkown command " + args[0]);
        }
    }
}