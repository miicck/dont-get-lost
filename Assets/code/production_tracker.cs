using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class production_tracker
{
    const int BINS = 60; // How many bins into the past to store
    const float bin_length = 1f; // How long a bin is (in seconds)

    // The time at the end of the last bin (i.e the time
    // at * in the below diagram of bins, where | is the
    // current time:
    // [  ][  ][  ][ | ]*
    static float bin_end_time = Time.realtimeSinceStartup;

    static Dictionary<string, int[]> bins = new Dictionary<string, int[]>();

    public static void register_product(item product, int count = 1)
    {
        // Nothing actually made
        if (product == null || count < 1) return;

        // Ensure bins line up with the time
        validate_bins();

        if (!bins.TryGetValue(product.name, out int[] array))
        {
            // No data for this product, create data array
            array = new int[BINS];
            bins[product.name] = array;
        }

        // Add the product to the latest bin
        array[array.Length - 1] += count;
    }

    public static void register_product(string name, int count=1)
    {
        register_product(Resources.Load<item>("items/" + name), count);
    }

    static void validate_bins()
    {
        // Shift bins until the current time is somewhere in the last bin
        while (Time.realtimeSinceStartup > bin_end_time + bin_length)
        {
            // Shift all bin times back by one bin length
            bin_end_time += bin_length;
            foreach (var kv in bins)
            {
                for (int i = 0; i < BINS - 1; ++i)
                    kv.Value[i] = kv.Value[i + 1];
                kv.Value[BINS - 1] = 0; // Clear the end bin
            }
        }
    }

    public struct current_prod_info
    {
        public int rate_per_min;
        public int rate_per_min_60sec_av;
    }

    public static Dictionary<item, current_prod_info> current_production()
    {
        validate_bins();

        float current_bin_amt = (Time.realtimeSinceStartup - bin_end_time) / bin_length;
        float previous_bin_amt = 1 - current_bin_amt;

        if (current_bin_amt > 1 || current_bin_amt < 0)
            throw new System.Exception("Bins not validated properly!");

        Dictionary<item, current_prod_info> ret = new Dictionary<item, current_prod_info>();
        foreach (var kv in bins)
        {
            var itm = Resources.Load<item>("items/" + kv.Key);
            if (itm == null) throw new System.Exception("Unkown item : " + kv.Key);

            // Work out production rates (note that the current bin has only been recording
            // for bin_length * current_bin_amt seconds, wheras the previous bin has recorded
            // for the full bin_length).
            float current_bin_prod = kv.Value[BINS - 1] / (bin_length * current_bin_amt);
            float previous_bin_prod = kv.Value[BINS - 2] / bin_length;

            current_prod_info info;
            info.rate_per_min = (int)(60f * (current_bin_prod * current_bin_amt + previous_bin_prod * previous_bin_amt));

            // Get 5 second average (not including current bin)
            int to_average = Mathf.CeilToInt(60f / bin_length);
            if (to_average > BINS - 1) to_average = BINS - 1;
            float av_rate = 0;
            for (int i = 0; i < to_average; ++i)
                av_rate += kv.Value[BINS - 2 - i];
            av_rate /= bin_length * to_average;
            info.rate_per_min_60sec_av = (int)(60 * av_rate);

            ret[itm] = info;
        }
        return ret;
    }

    public static string current_production_info()
    {
        string ret = "";
        foreach (var kv in current_production())
            ret += kv.Value.rate_per_min + " " + kv.Key.plural + "/min\n";
        return ret;
    }

    static RectTransform ui;
    static UnityEngine.UI.ScrollRect scroll_rect;
    static RectTransform entry_template;
    static float last_ui_update_time = -1;

    public static void set_ui_state(bool state)
    {
        if (ui == null)
        {
            // Create the ui
            ui = Resources.Load<RectTransform>("ui/production_info").inst(game.canvas.transform);
            ui.anchoredPosition = Vector2.zero;

            // Get the entry template (which remains disabled)
            scroll_rect = ui.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            entry_template = (RectTransform)scroll_rect.content.Find("entry_template");
            entry_template.gameObject.SetActive(false);
        }

        if (state) update_ui(true);
        ui.gameObject.SetActive(state);
    }

    public static void update_ui(bool force = false)
    {
        if (ui == null || !ui.gameObject.activeInHierarchy) return;

        if (!force && Time.realtimeSinceStartup < last_ui_update_time + 1f)
            return; // Only update every now and then, or when forced

        last_ui_update_time = Time.realtimeSinceStartup;

        // Remove the old entries
        foreach (RectTransform t in scroll_rect.content)
        {
            if (t == entry_template) continue;
            t.gameObject.SetActive(false);
            Object.Destroy(t.gameObject);
        }

        foreach (var kv in current_production())
        {
            // Create the entries for each item in production
            var entry = entry_template.inst(scroll_rect.content);
            entry.gameObject.SetActive(true);

            entry.Find("sprite").GetComponent<UnityEngine.UI.Image>().sprite = kv.Key.sprite;
            entry.Find("name").GetComponent<UnityEngine.UI.Text>().text = kv.Key.plural;
            entry.Find("rate").GetComponent<UnityEngine.UI.Text>().text = kv.Value.rate_per_min.ToString();
            entry.Find("rate_60s").GetComponent<UnityEngine.UI.Text>().text = kv.Value.rate_per_min_60sec_av.ToString();
        }
    }
}
