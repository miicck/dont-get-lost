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

    public weather_effect effect;

    static Dictionary<weather_effect, weather_effect> active_effects = new Dictionary<weather_effect, weather_effect>();

    void mix(List<weather> weathers, List<float> weights)
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

        var effect_container = player.current.transform;

        // Create new weighted weather effects
        for (int i = 0; i < weathers.Count; ++i)
        {
            var e = weathers[i].effect;
            if (e == null) continue;

            var w = weights[i];

            // Effect is already active
            if (active_effects.TryGetValue(e, out weather_effect effect))
            {
                effect.weight = w;
                continue;
            }

            effect = active_effects[e] = e.inst();
            effect.transform.SetParent(effect_container);
            effect.transform.localPosition = Vector3.zero;
            effect.transform.localRotation = Quaternion.identity;
            effect.weight = w;
        }
    }

    private void Update()
    {
        if (weather_nodes.Count == 0)
            return; // No weather events to process

        if (weather_nodes.Count == 1)
            throw new System.Exception("Too few weather nodes!");

        var from = weather_nodes[0];
        var to = weather_nodes[1];

        if (to.time < from.time)
            throw new System.Exception("Weather event times in wrong order!");

        float x = (Time.time - from.time) / (to.time - from.time);

        if (x >= 1)
        {
            // We've reached full force on the next weather event
            x = 1;

            // Remove the previous weather event
            weather_nodes.RemoveAt(0);

            if (weather_nodes.Count == 1)
            {
                // We've reached the last weather node, remove
                // it as we no longer need to change weather.
                weather_nodes.RemoveAt(0);
            }

            // Overwrite weather with the next weather
            replace_current_weather(to.weather.inst());
        }

        // Interpolate current weather between from and to
        mix(new List<weather> { from.weather, to.weather }, new List<float> { 1 - x, x });
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
                replace_current_weather(Resources.Load<weather>("weathers/sun").inst());
            return _weather;
        }
        set
        {
            _weather.mix(new List<weather> { value }, new List<float> { 1.0f });
        }
    }
    static weather _weather;

    static void replace_current_weather(weather new_weather)
    {
        if (_weather != null)
            Destroy(_weather.gameObject);
        _weather = new_weather;
        _weather.name = "current_weather";
        _weather.transform.SetParent(FindObjectOfType<lighting>()?.transform);
    }

    static weather last;

    struct weather_node
    {
        public weather weather;
        public float time;
    }

    static List<weather_node> weather_nodes = new List<weather_node>();

    public static void queue_weather_event(weather weather, float time_from_now)
    {
        weather_node e = new weather_node
        {
            weather = weather,
            time = Time.time + time_from_now
        };

        if (weather_nodes.Count == 0)
        {
            last = current.inst();
            last.name = "last_weather";
            last.transform.SetParent(current.transform);
            weather_nodes.Add(new weather_node
            {
                time = Time.time,
                weather = last
            });

            weather_nodes.Add(e);
            return;
        }

        if (weather_nodes.Count == 1)
            throw new System.Exception("Only one weather node present on add!");

        for (int i = 0; i < weather_nodes.Count; ++i)
            if (weather_nodes[i].time > e.time)
            {
                weather_nodes.Insert(i, e);
                return;
            }

        weather_nodes.Add(e);
    }
}
