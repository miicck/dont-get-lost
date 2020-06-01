using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class options_menu_bool : options_menu_option
{
    public string option_name = "water_reflections";
    public bool default_value = true;
    public UnityEngine.UI.Toggle toggle;

    private void Start()
    {
        toggle.onValueChanged.AddListener((b) =>
        {
            options_menu.set_bool(option_name, b);
        });

        toggle.onValueChanged.Invoke(PlayerPrefs.GetInt(option_name) > 0);
    }

    public override void load_default()
    {
        toggle.onValueChanged.Invoke(default_value);
    }

    public override void initialize_option()
    {
        options_menu.set_bool(option_name, PlayerPrefs.GetInt(option_name) > 0);
    }
}