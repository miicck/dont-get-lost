using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A boolean value that can be set
/// via the options menu. </summary>
public class options_menu_bool : options_menu_option
{
    public string option_name = "water_reflections";
    public bool default_value = true;
    public bool optimized_value = true;
    public UnityEngine.UI.Toggle toggle;
    public GameObject suboptions;

    private void Start()
    {
        // The option is simply syncronised with the toggle
        toggle.onValueChanged.AddListener((b) =>
        {
            options_menu.set_bool(option_name, b);
            if (suboptions != null)
                suboptions.gameObject.SetActive(b);
        });

        // Initialize the toggle to the saved value
        toggle.isOn = PlayerPrefs.GetInt(option_name) > 0;
    }

    public override void load_default()
    {
        // Set the toggle (and therefore the option via the 
        // toggle.onValueChanged method) to the default value
        toggle.isOn = default_value;
    }

    public override void load_optimized()
    {
        toggle.isOn = optimized_value;
    }

    public override void initialize_option()
    {
        // Load the option from saved value
        options_menu.set_bool(option_name, PlayerPrefs.GetInt(option_name) > 0);
    }
}