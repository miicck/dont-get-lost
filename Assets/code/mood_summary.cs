using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mood_summary : MonoBehaviour
{
    public UnityEngine.UI.Text summary_text;

    void update_summary_text()
    {
        Dictionary<string, int> total_effects = new Dictionary<string, int>();

        foreach (var s in settler.all_settlers())
        {
            foreach (var me in mood_effect.get_all(s))
            {
                if (!total_effects.TryGetValue(me.display_name, out int total))
                    total = 0;
                total += me.delta_mood;
                total_effects[me.display_name] = total;
            }
        }

        var pairs = new List<KeyValuePair<string, int>>(total_effects);
        pairs.Sort((a, b) => a.Value.CompareTo(b.Value));

        int max_key_length = 0;
        foreach (var kv in pairs)
            if (kv.Key.Length > max_key_length)
                max_key_length = kv.Key.Length;

        summary_text.text = "";

        string title = "Mood effect";
        string headers = title + new string(' ', max_key_length + 1 - title.Length) + "Total mood contribution\n";
        summary_text.text += headers;
        summary_text.text += new string('-', headers.Length) + "\n";

        foreach (var kv in pairs)
            summary_text.text += kv.Key + new string(' ', max_key_length + 1 - kv.Key.Length) + kv.Value + "\n";
    }

    private void OnEnable()
    {
        update_summary_text();
    }
}
