using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class console : MonoBehaviour
{
    static console current;

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

    UnityEngine.UI.InputField input;

    void Start()
    {
        input = GetComponent<UnityEngine.UI.InputField>();
        current = this;
        open = false;

        input.onEndEdit.AddListener((string command) =>
        {
            process_command(command);
            open = false;
            input.text = "";
        });
    }

    bool console_error(string msg)
    {
        popup_message.create(msg);
        return false;
    }

    bool process_command(string command)
    {
        var args = command.Split(null);
        
        switch(args[0])
        {
            case "give":

                if (args.Length < 3) return console_error("Not enough arguments!");

                if (!int.TryParse(args[1], out int count))
                    return console_error("Could not parse quantity from " + args[1]);

                if (Resources.Load<item>("items/" + args[2]) == null)
                    return console_error("Could not identify item " + args[2]);

                player.current.inventory.add(args[2], count);
                return true;

            default:
                return console_error("Unkown command " + args[0]);
        }
    }
}