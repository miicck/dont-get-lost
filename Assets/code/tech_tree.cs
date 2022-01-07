using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class tech_tree : networked
{
    public override float network_radius()
    {
        // The tech tree is always loaded
        return Mathf.Infinity;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    public static RectTransform generate_tech_tree()
    {
        const int SPACING = 128;

        // Load the technologies + init coordinates
        Dictionary<technology, int[]> coords = new Dictionary<technology, int[]>();
        foreach (var t in Resources.LoadAll<technology>("technologies"))
            coords[t] = new int[] { 0, 0 };


        // Work out which row each technology belongs on
        int iter = 0;
        while (true)
        {
            if (++iter > 1000)
            {
                Debug.LogError("Could not identify technology rows in 1000 iterations, are cycles present?");
                break;
            }

            bool changed = false;
            foreach (var kv in coords)
            {
                int row_t = kv.Value[1];
                foreach (var td in kv.Key.depends_on)
                {
                    int row_td = coords[td][1];
                    if (row_td >= row_t)
                    {
                        changed = true;
                        row_t = kv.Value[1] = row_td + 1;
                    }
                }
            }
            if (!changed) break;
        }

        // Work out which column each technology belongs on
        Dictionary<int, int> row_progress = new Dictionary<int, int>();
        foreach (var kv in coords)
            row_progress[kv.Value[1]] = 0;

        foreach (var kv in coords)
        {
            var prog = row_progress[kv.Value[1]];
            kv.Value[0] = prog;
            row_progress[kv.Value[1]] = prog + 1;
        }

        // Create the tech tree template object
        var ui = Resources.Load<RectTransform>("ui/tech_tree").inst();
        var content = ui.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;
        var tech_template = content.GetChild(0);

        Dictionary<technology, RectTransform> ui_elements = new Dictionary<technology, RectTransform>();
        foreach (var kv in coords)
        {
            var t = kv.Key;
            var tt = tech_template.inst().GetComponent<RectTransform>();
            tt.SetParent(tech_template.parent);
            tt.get_child_with_name<UnityEngine.UI.Image>("sprite").sprite = t.sprite;
            tt.get_child_with_name<UnityEngine.UI.Text>("text").text = t.name.Replace('_', '\n').capitalize();
            tt.name = t.name;

            // Position according to coordinates
            tt.anchoredPosition = new Vector2((kv.Value[0] + 1) * SPACING, -(kv.Value[1] + 1) * SPACING);

            // Save UI element for each technology
            ui_elements[t] = tt;
        }

        // Draw dependence arrows
        foreach (var kv in coords)
        {
            foreach (var t in kv.Key.depends_on)
            {
                // Create arrow
                var arr = new GameObject("arrow").AddComponent<RectTransform>();
                arr.anchorMin = new Vector2(0, 1);
                arr.anchorMax = new Vector2(0, 1);
                var img = arr.gameObject.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(0, 0, 0, 0.4f);

                // Parent/position/size arrow
                arr.SetParent(tech_template.parent);
                arr.SetAsFirstSibling();
                Vector2 from = ui_elements[kv.Key].anchoredPosition;
                Vector2 to = ui_elements[t].anchoredPosition;
                arr.anchoredPosition = (from + to) / 2f;
                arr.sizeDelta = new Vector2(2, (to - from).magnitude);
                arr.up = to - from;
            }
        }

        tech_template.SetParent(null);
        Destroy(tech_template.gameObject);

        ui.SetParent(game.canvas.transform);
        ui.anchoredPosition = Vector3.zero;
        return ui;
    }
}