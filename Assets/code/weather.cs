using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class weather : MonoBehaviour
{
    public Color day_sun_color;
    public Color dawn_sun_color;
    public Color dusk_sun_color;
    public Color night_sun_color;

    public float daytime_ambient_brightness = 1f;
    public float nighttime_ambient_brightness = 1f;
    public Color daytime_ambient_color = Color.white;
    public Color nighttime_ambient_color = Color.white;

    public float daytime_saturation = 1f;
    public float nighttime_saturation = 0.5f;

    public void mix(List<weather> weathers, List<float> weights)
    {
        day_sun_color = average_color(get_list(weathers, (w) => w.day_sun_color), weights);
        dawn_sun_color = average_color(get_list(weathers, (w) => w.dawn_sun_color), weights);
        dusk_sun_color = average_color(get_list(weathers, (w) => w.dusk_sun_color), weights);
        night_sun_color = average_color(get_list(weathers, (w) => w.night_sun_color), weights);

        daytime_ambient_brightness = average_float(get_list(weathers, (w) => w.daytime_ambient_brightness), weights);
        nighttime_ambient_brightness = average_float(get_list(weathers, (w) => w.nighttime_ambient_brightness), weights);
        daytime_ambient_color = average_color(get_list(weathers, (w) => w.daytime_ambient_color), weights);
        nighttime_ambient_color = average_color(get_list(weathers, (w) => w.nighttime_ambient_color), weights);

        daytime_saturation = average_float(get_list(weathers, (w) => w.daytime_saturation), weights);
        nighttime_saturation = average_float(get_list(weathers, (w) => w.nighttime_saturation), weights);
    }

    delegate T get_object<T>(weather w);
    List<T> get_list<T>(List<weather> ws, get_object<T> get)
    {
        List<T> result = new List<T>();
        foreach (var w in ws)
            result.Add(get(w));
        return result;
    }

    Color average_color(List<Color> colors, List<float> weights)
    {
        float total_weight = 0;
        foreach (var f in weights)
            total_weight += f;

        Color result = Color.black;
        for (int i = 0; i < colors.Count; ++i)
        {
            var c = colors[i];
            var w = weights[i];
            result.r += w * c.r / total_weight;
            result.g += w * c.g / total_weight;
            result.b += w * c.b / total_weight;
        }
        return result;
    }

    float average_float(List<float> floats, List<float> weights)
    {
        float total_weight = 0f;
        float result = 0f;
        for (int i = 0; i < floats.Count; ++i)
        {
            var f = floats[i];
            var w = weights[i];
            result += f * w;
            total_weight += w;
        }
        return result / total_weight;
    }

    public static weather current
    {
        get
        {
            if (_weather == null)
            {
                _weather = Resources.Load<weather>("weathers/sunny").inst();
                _weather.name = "current_weather";
                _weather.transform.SetParent(FindObjectOfType<lighting>()?.transform);
            }
            return _weather;
        }
        set
        {
            _weather.mix(new List<weather> { value }, new List<float> { 1.0f });
        }
    }
    static weather _weather;
}
