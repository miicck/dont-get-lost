using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mood_summary : MonoBehaviour
{
    public UnityEngine.UI.Text summary_text;

    void update_summary_text()
    {

        var all_settlers = settler.all_settlers();
        if (all_settlers.Count == 0)
        {
            summary_text.text = "No settlers";
            return;
        }

        Dictionary<string, int> total_effects = new Dictionary<string, int>();

        foreach (var s in all_settlers)
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

        // Display average quantities
        float average_mood = 0;
        foreach (var s in all_settlers)
            average_mood += s.total_mood();
        average_mood /= all_settlers.Count;

        summary_text.text += "Average town mood: " + average_mood;

        // Display info about settlers planning on leaving
        string leaving_info = "";
        foreach (var s in all_settlers)
            if (s.reason_for_leaving != null)
                leaving_info += "    " +
                    s.name.capitalize() + ": " +
                    s.reason_for_leaving +
                    " (" + Mathf.RoundToInt(s.time_had_reason_to_leave) + "s)\n";
        if (leaving_info.Length > 0)
            summary_text.text += "\n\nSettlers planning on leaving:\n" + leaving_info;

        // Display mood effects by total contribution 
        summary_text.text += "\n\n";
        string title = "Mood effect";
        string headers = title + new string(' ', max_key_length + 1 - title.Length) + "Total mood contribution\n";
        summary_text.text += headers;
        summary_text.text += new string('-', headers.Length) + "\n";

        foreach (var kv in pairs)
            summary_text.text += kv.Key + new string(' ', max_key_length + 1 - kv.Key.Length) + kv.Value + "\n";

        // Display info about saddest settler
        summary_text.text += "\n\n";
        var saddest_settler = utils.find_to_min(all_settlers, (s) => s.total_mood());
        string settler_header = "Saddest settler: " + saddest_settler.name + " (mood = " + saddest_settler.total_mood() + ")\n";
        summary_text.text += settler_header;
        summary_text.text += new string('-', headers.Length) + "\n";

        foreach (var me in mood_effect.get_all(saddest_settler))
            summary_text.text += me.display_name + new string(' ', max_key_length + 1 - me.display_name.Length) + me.delta_mood + "\n";
    }

    private void OnEnable()
    {
        update_summary_text();
    }
}
