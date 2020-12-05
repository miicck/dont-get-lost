using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class color_selector : MonoBehaviour
{
    public UnityEngine.UI.Slider red_slider;
    public UnityEngine.UI.Slider green_slider;
    public UnityEngine.UI.Slider blue_slider;
    public UnityEngine.UI.Image color_preview;

    public delegate void on_change_func();
    public on_change_func on_change;

    public Color color
    {
        get => color_preview.color;
        set
        {
            color_preview.color = value;

            if (Mathf.Abs(red_slider.value - value.r) > 10e-4)
                red_slider.value = value.r;

            if (Mathf.Abs(green_slider.value - value.g) > 10e-4)
                green_slider.value = value.g;

            if (Mathf.Abs(blue_slider.value - value.b) > 10e-4)
                blue_slider.value = value.b;

            on_change?.Invoke();
        }
    }

    private void Start()
    {
        red_slider.onValueChanged.AddListener((e) =>
        {
            color = new Color(red_slider.value, color.g, color.b);
        });

        green_slider.onValueChanged.AddListener((e) =>
        {
            color = new Color(color.r, green_slider.value, color.b);
        });

        blue_slider.onValueChanged.AddListener((e) =>
        {
            color = new Color(color.r, color.g, blue_slider.value);
        });

        color = color;
    }
}
