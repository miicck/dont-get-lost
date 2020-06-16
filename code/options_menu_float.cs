using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A floating point value that can be changed 
/// via the options menu. </summary>
public class options_menu_float : options_menu_option
{
    public string option_name = "contrast";
    public float default_value = 0;
    public UnityEngine.UI.Slider slider;
    public UnityEngine.UI.InputField input;

    private void Start()
    {
        // The input field sets the slider value
        input.onValueChanged.AddListener((s) =>
        {
            float val;
            if (float.TryParse(s, out val))
            {
                // Successfulyl parsed a float, clamp it
                // to the slider range and set the slider value
                val = Mathf.Clamp(val, slider.minValue, slider.maxValue);
                input.text = "" + val;
                options_menu.set_float(option_name, val);
                slider.value = val;
            }
            else 
                // Failed to parse a float, reset the text to
                // the slider value.
                input.text = "" + slider.value;
        });

        // The slider sets the actual option, and the
        // value displayed in the input field
        slider.onValueChanged.AddListener((val) =>
        {
            options_menu.set_float(option_name, val);
            input.text = "" + val;
        });

        // Load the slider (and therefore also the input field) with the saved value
        slider.value = PlayerPrefs.GetFloat(option_name, default_value);
    }

    private void OnEnable()
    {
        input.text = "" + slider.value;
    }

    public override void initialize_option()
    {
        // Initialize the option to the saved value
        options_menu.set_float(option_name, PlayerPrefs.GetFloat(option_name, default_value));
    }

    public override void load_default()
    {
        // Set the slider (and therefore the option and input field text)
        // to the default value
        slider.value = default_value;
    }
}