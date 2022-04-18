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

        summary_text.text = "";
        foreach (var kv in pairs)
            summary_text.text += kv.Key + " " + kv.Value + "\n";
    }

    private void Update()
    {
        if (Time.frameCount % 11 == 0)
            update_summary_text();
    }
}
