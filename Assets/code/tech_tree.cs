using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class tech_tree : networked
{
    //############//
    // NETWORKING //
    //############//

    public networked_variables.net_string_counts research_progress;
    public networked_variables.net_string currently_researching;

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        research_progress = new networked_variables.net_string_counts();
        currently_researching = new networked_variables.net_string(default_value: "");

        currently_researching.on_change = () =>
        {
            if (technology.is_valid_name(name))
            {
                Debug.LogError("Tried to set unknown research: " + name);
                return;
            }
            update_tech_tree_ui();
        };

        research_progress.on_change = update_tech_tree_ui;
    }

    // The tech tree is always loaded
    public override float network_radius() => Mathf.Infinity;
    private void Start() => loaded_tech_tree = this;

    //##############//
    // STATIC STUFF //
    //##############//

    static tech_tree loaded_tech_tree;
    static RectTransform tech_tree_ui;

    public static void set_research(string name)
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to set research before tech tree loaded");
            return;
        }

        loaded_tech_tree.currently_researching.value = name;
    }

    public static void perform_research(int amount)
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to perform research before tech tree loaded");
            return;
        }

        string topic = loaded_tech_tree.currently_researching.value;
        perform_research(topic, amount);
    }

    public static void perform_research(string topic, int amount)
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to perform research before tech tree loaded");
            return;
        }

        loaded_tech_tree.research_progress[topic] =
            Mathf.Min(100, loaded_tech_tree.research_progress[topic] + amount);

        // Unassign completed research
        if (current_research_complete())
            loaded_tech_tree.currently_researching.value = "";
    }

    public static void unlock_all_research()
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried research everything before tech tree loaded");
            return;
        }

        foreach (var t in technology.all)
            loaded_tech_tree.research_progress[t.name] = 100;
    }

    public static bool research_project_set()
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to check if research is set before tech tree loaded");
            return false;
        }

        return loaded_tech_tree.currently_researching.value != "";
    }

    public static string reseach_project()
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to get research project before tech tree loaded");
            return "";
        }

        return loaded_tech_tree.currently_researching.value.Replace('_', ' ');
    }

    public static bool current_research_complete()
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to check if current research was complete before tech tree loaded");
            return false;
        }

        return research_complete(loaded_tech_tree.currently_researching.value);
    }

    public static bool research_complete(string name)
    {
        name = name.Replace(' ', '_');

        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to check if research was complete before tech tree loaded");
            return false;
        }

        return
            loaded_tech_tree.research_progress.contains_key(name) &&
            loaded_tech_tree.research_progress[name] >= 100;
    }

    static void update_tech_tree_ui()
    {
        if (loaded_tech_tree == null)
            return;

        // No ui to update
        if (tech_tree_ui == null)
            return;

        var content = tech_tree_ui.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;
        foreach (RectTransform c in content)
        {
            var tech = technology.load(c.name);
            if (tech == null)
                continue;

            int progress = 0;
            if (loaded_tech_tree.research_progress.contains_key(c.name))
                progress = loaded_tech_tree.research_progress[c.name];

            var button = c.get_child_with_name<UnityEngine.UI.Button>("research_button");
            var button_text = button.GetComponentInChildren<UnityEngine.UI.Text>();

            if (loaded_tech_tree.currently_researching.value == c.name)
            {
                // Current research
                button_text.text = progress + "/100";
                button.interactable = false;
            }
            else
            {
                if (tech.complete)
                {
                    // Completed research
                    button_text.text = "Complete";
                    button.interactable = false;
                }
                else if (tech.prerequisites_complete)
                {
                    // Available research
                    button_text.text = "Research";
                    button.interactable = true;
                }
                else
                {
                    // Prerequisites not met
                    button_text.text = "Unavailable";
                    button.interactable = false;
                }
            }
        }
    }

    static int tech_tree_distance_cost(int x1, int x2)
    {
        return (x1 - x2) * (x1 - x2);
    }

    public static RectTransform generate_tech_tree()
    {
        const int SPACING = 128;
        const int ARROW_WIDTH = 4;


        if (tech_tree_ui != null)
            return tech_tree_ui;

        // Load the technologies + init coordinates
        Dictionary<technology, int[]> coords = new Dictionary<technology, int[]>();
        foreach (var t in technology.all)
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
        // Initialize to one slot per technology on each row
        Dictionary<int, int> row_counts = new Dictionary<int, int>();
        foreach (var kv in coords)
        {
            if (!row_counts.TryGetValue(kv.Value[1], out int count)) count = 0;
            kv.Value[0] = count;
            row_counts[kv.Value[1]] = count + 1;
        }

        // Setup technology matrix
        int max_col = 0;
        int max_row = 0;
        foreach (var kv in coords)
        {
            max_col = Mathf.Max(max_col, kv.Value[0]);
            max_row = Mathf.Max(max_row, kv.Value[1]);
        }
        var matrix = new technology[max_col + 1, max_row + 1];
        foreach (var kv in coords)
            matrix[kv.Value[0], kv.Value[1]] = kv.Key;

        // Setup dependance dict
        Dictionary<technology, List<technology>> children = new Dictionary<technology, List<technology>>();
        foreach (var kv in coords)
            foreach (var parent in kv.Key.depends_on)
            {
                if (!children.ContainsKey(parent))
                    children[parent] = new List<technology>();
                children[parent].Add(kv.Key);
            }
        foreach (var kv in coords)
            if (!children.ContainsKey(kv.Key))
                children[kv.Key] = new List<technology>();


        // Perform swaps on the matrix to reduce the average distance
        // between technologies and their dependants
        iter = 0;
        while (true)
        {
            if (++iter > 1000)
            {
                Debug.LogError("Could not identify optimal tech columns after 1000 iterations!");
                break;
            }

            bool swap_perfomed = false;

            // Swap parents to reduce child distance
            for (int row = 0; row < matrix.GetLength(1); ++row)
                for (int col = 1; col < matrix.GetLength(0); ++col)
                {
                    technology left = matrix[col - 1, row];
                    technology right = matrix[col, row];

                    int score_unswapped = 0;
                    int score_swapped = 0;

                    if (left != null)
                        foreach (var c in children[left])
                        {
                            score_unswapped += tech_tree_distance_cost(coords[c][0], (col - 1));
                            score_swapped += tech_tree_distance_cost(coords[c][0], col);
                        }

                    if (right != null)
                        foreach (var c in children[right])
                        {
                            score_unswapped += tech_tree_distance_cost(coords[c][0], col);
                            score_swapped += tech_tree_distance_cost(coords[c][0], (col - 1));
                        }

                    if (score_swapped > score_unswapped)
                        continue; // Swap would be worse

                    if (score_swapped == score_unswapped)
                        continue; // Swap would be the same - perhaps randomly swap?

                    // Perform swap
                    matrix[col, row] = left;
                    if (left != null) coords[left][0] = col;

                    matrix[col - 1, row] = right;
                    if (right != null) coords[right][0] = col - 1;

                    swap_perfomed = true;
                }

            // Swap children to reduce parent distance
            for (int row = 1; row < matrix.GetLength(1); ++row) // Start at row 1 because row 0 has no parents
                for (int col = 1; col < matrix.GetLength(0); ++col)
                {
                    technology left = matrix[col - 1, row];
                    technology right = matrix[col, row];

                    int score_unswapped = 0;
                    int score_swapped = 0;

                    if (left != null)
                        foreach (var p in left.depends_on)
                        {
                            score_unswapped += tech_tree_distance_cost(coords[p][0], (col - 1));
                            score_swapped += tech_tree_distance_cost(coords[p][0], col);
                        }

                    if (right != null)
                        foreach (var p in right.depends_on)
                        {
                            score_unswapped += tech_tree_distance_cost(coords[p][0], col);
                            score_swapped += tech_tree_distance_cost(coords[p][0], (col - 1));
                        }

                    if (score_swapped > score_unswapped)
                        continue; // Swap would be worse

                    if (score_swapped == score_unswapped)
                        continue; // Swap would be the same - perhaps randomly swap?

                    // Perform swap
                    matrix[col, row] = left;
                    if (left != null) coords[left][0] = col;

                    matrix[col - 1, row] = right;
                    if (right != null) coords[right][0] = col - 1;

                    swap_perfomed = true;
                }


            if (!swap_perfomed)
                break;
        }

        // Create the tech tree template object
        tech_tree_ui = Resources.Load<RectTransform>("ui/tech_tree").inst();
        var content = tech_tree_ui.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;
        var tech_template = content.GetChild(0);

        Dictionary<technology, RectTransform> ui_elements = new Dictionary<technology, RectTransform>();
        foreach (var kv in coords)
        {
            var t = kv.Key;
            var tt = tech_template.inst().GetComponent<RectTransform>();
            tt.SetParent(tech_template.parent);
            tt.get_child_with_name<UnityEngine.UI.Image>("sprite").sprite = t.sprite;
            tt.get_child_with_name<UnityEngine.UI.Text>("text").text = t.name.Replace('_', '\n').capitalize();
            tt.get_child_with_name<UnityEngine.UI.Button>("research_button").onClick.AddListener(() => set_research(t.name));
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
                arr.sizeDelta = new Vector2(ARROW_WIDTH, (to - from).magnitude);
                arr.up = to - from;
            }
        }

        tech_template.SetParent(null);
        Destroy(tech_template.gameObject);

        tech_tree_ui.SetParent(game.canvas.transform);
        tech_tree_ui.set_left(128);
        tech_tree_ui.set_right(128);
        tech_tree_ui.set_top(128);
        tech_tree_ui.set_bottom(128);

        update_tech_tree_ui();

        return tech_tree_ui;
    }
}
