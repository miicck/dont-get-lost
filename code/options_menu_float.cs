using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class options_menu_float : MonoBehaviour
{
    public string option_name = "contrast";
    public float default_value = 0;
    public UnityEngine.UI.Slider slider;
    public UnityEngine.UI.InputField input;

    private void Start()
    {
        input.onValueChanged.AddListener((s) =>
        {
            float val;
            if (float.TryParse(s, out val))
            {
                val = Mathf.Clamp(val, -100, 100);
                input.text = "" + val;
                options_menu.set_float(option_name, val);
                slider.value = val;
            }
            else input.text = "" + slider.value;
        });

        slider.onValueChanged.AddListener((val) =>
        {
            options_menu.set_float(option_name, val);
            input.text = "" + val;
        });

        slider.onValueChanged.Invoke(PlayerPrefs.GetFloat(option_name, default_value));
    }

    public void init()
    {
        options_menu.set_float(option_name, PlayerPrefs.GetFloat(option_name, default_value));
    }

    public void load_default()
    {
        slider.onValueChanged.Invoke(default_value);
    }
}
