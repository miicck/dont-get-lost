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

    public override void on_create()
    {
        // Ensure only one tech tree exists
        utils.delete_all_but_oldest(FindObjectsOfType<tech_tree>(), callback: (n) =>
        {
            var tt = (tech_tree)n;
            foreach (var kv in tt.research_progress)
                if (kv.Value > 0)
                {
                    Debug.LogError("Found multiple tech trees with non-zero research progress!");
                    return;
                }
        });
    }

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

    public static int population_cap => town_level.current_population_cap;

    public static void set_research(technology t)
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to set research before tech tree loaded");
            return;
        }

        foreach (var item_ingredient in t.GetComponentsInChildren<item_ingredient>())
            player.current.inventory.remove(item_ingredient.item, item_ingredient.count);

        loaded_tech_tree.currently_researching.value = t.name;
    }

    public static int get_research_amount(string t)
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to get research amount before tech tree loaded");
            return 0;
        }

        return loaded_tech_tree.research_progress[t.Replace(' ', '_')];
    }

    public static int get_research_percent(string t)
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to get research percent before tech tree loaded");
            return 0;
        }

        var tech = technology.load(t);
        if (tech == null)
        {
            Debug.LogError("Tried to get reserach percent of unkown technology: " + t);
            return 0;
        }

        return get_research_amount(t) * 100 / tech.research_time;
    }

    public static int get_research_amount(technology t) => get_research_amount(t.name);

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
        if (topic.Length == 0)
            return; // Researching the "" topic

        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to perform research before tech tree loaded");
            return;
        }

        var t = Resources.Load<technology>("technologies/" + topic);
        if (t == null)
        {
            Debug.LogError("Tried to perform unkown research: " + topic);
            return;
        }

        loaded_tech_tree.research_progress[topic] =
            Mathf.Min(t.research_time, loaded_tech_tree.research_progress[topic] + amount);

        // Unassign completed research
        if (current_research_complete())
        {
            temporary_object.create(60f).gameObject.add_pinned_message("Research topic complete", Color.green);
            popup_message.create("Completed research: " + topic);
            loaded_tech_tree.currently_researching.value = "";
        }
    }

    public static void unlock_all_research()
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried research everything before tech tree loaded");
            return;
        }

        foreach (var t in technology.all)
            loaded_tech_tree.research_progress[t.name] = t.research_time;
    }

    public static void lock_all_research()
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to un-research everythong before tech tree loaded");
            return;
        }

        foreach (var t in technology.all)
            loaded_tech_tree.research_progress[t.name] = 0;
    }

    public static bool research_project_set()
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to check if research is set before tech tree loaded");
            return false;
        }

        return current_research_technology() != null;
    }

    public static string current_research_project()
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to get research project before tech tree loaded");
            return "";
        }

        return loaded_tech_tree.currently_researching.value.Replace('_', ' ');
    }

    public static bool researching() => current_research_project().Length > 0;

    public static technology current_research_technology()
    {
        if (loaded_tech_tree == null)
        {
            Debug.LogError("Tried to get research project before tech tree loaded");
            return null;
        }

        if (loaded_tech_tree.currently_researching.value == "")
            return null;

        foreach (var t in technology.all)
            if (t.name == loaded_tech_tree.currently_researching.value)
                return t;

        return null;
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

        var t = Resources.Load<technology>("technologies/" + name);
        if (t == null)
        {
            Debug.LogError("Tried to check compeltion of unkown research: " + name);
            return false;
        }

        return
            loaded_tech_tree.research_progress.contains_key(name) &&
            loaded_tech_tree.research_progress[name] >= t.research_time;
    }

    public static bool research_complete(technology t) => research_complete(t.name);

    public static void update_tech_tree_ui()
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
                button_text.text = progress + "/" + tech.research_time;
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
                else if (!tech.prerequisites_complete)
                {
                    // Prerequisites not met
                    button_text.text = "Requirements not met";
                    button.interactable = false;
                }
                else if (!tech.required_items_available)
                {
                    // Items not available
                    button_text.text = "Items missing";
                    button.interactable = false;
                }
                else
                {
                    // Available research
                    button_text.text = "Research";
                    button.interactable = true;
                }
            }
        }
    }

    public class technology_ui : MonoBehaviour, IMouseTextUI
    {
        public string text;
        public string mouse_ui_text() => text;
    }

    public static RectTransform generate_tech_tree()
    {
        const int SPACING = 196;
        const int LINE_WIDTH = 4;

        if (tech_tree_ui != null)
            return tech_tree_ui;

        // Create the tech tree UI
        tech_tree_ui = Resources.Load<RectTransform>("ui/tech_tree").inst();

        // Load the technologies + init coordinates
        tech_tree_layout_engine layout = new force_layout_engine(technology.all);
        var coords = layout.evaluate_coordinates();
        var content = tech_tree_ui.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;
        var tech_template = content.GetChild(0);

        Dictionary<technology, RectTransform> ui_elements = new Dictionary<technology, RectTransform>();
        foreach (var kv in coords)
        {
            var t = kv.Key;
            var tt = tech_template.inst().GetComponent<RectTransform>();
            tt.SetParent(tech_template.parent);

            // Set spite
            var tech_sprite = tt.get_child_with_name<UnityEngine.UI.Image>("technology_sprite");
            tech_sprite.sprite = t.sprite;

            var tech_ui = tech_sprite.gameObject.AddComponent<technology_ui>();
            tech_ui.text = t.info();

            // Set title/button action
            var info_area = tt.Find("info_area");
            info_area.get_child_with_name<UnityEngine.UI.Text>("title").text = t.display_name.capitalize();
            info_area.get_child_with_name<UnityEngine.UI.Button>("research_button").onClick.AddListener(() => set_research(t));

            // Create a material requirement entry for each item ingredient
            var material_requirement_template = info_area.Find("item_requirement");
            foreach (var item_ingredient in t.GetComponentsInChildren<item_ingredient>())
            {
                var ing_requirement = material_requirement_template.inst();
                ing_requirement.transform.SetParent(material_requirement_template.transform.parent);
                ing_requirement.get_child_with_name<UnityEngine.UI.Image>("sprite").sprite = item_ingredient.item.sprite;
                ing_requirement.get_child_with_name<UnityEngine.UI.Text>("amount").text = item_ingredient.count.ToString();

                // Add mouse-over text
                ing_requirement.gameObject.AddComponent<technology_ui>().text = item_ingredient.item.display_name;
            }

            // Destroy material requirement template
            Destroy(material_requirement_template.gameObject);

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
                // Create line
                var line = new GameObject("line").AddComponent<RectTransform>();
                line.anchorMin = new Vector2(0, 1);
                line.anchorMax = new Vector2(0, 1);
                var img = line.gameObject.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(0, 0, 0, 0.4f);

                // Parent/position/size line
                line.SetParent(tech_template.parent);
                line.SetAsFirstSibling();
                Vector2 from = ui_elements[kv.Key].anchoredPosition;
                Vector2 to = ui_elements[t].anchoredPosition;
                line.anchoredPosition = (from + to) / 2f;
                line.sizeDelta = new Vector2(LINE_WIDTH, (to - from).magnitude);
                line.up = to - from;

                // Create arrow
                var arr = new GameObject("arrow").AddComponent<RectTransform>();
                arr.anchorMin = new Vector2(0, 1);
                arr.anchorMax = new Vector2(0, 1);
                img = arr.gameObject.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(0, 0, 0, 0.4f);
                img.sprite = Resources.Load<Sprite>("sprites/simple_arrow");

                // Parent/position/size line
                arr.SetParent(tech_template.parent);
                arr.SetAsFirstSibling();
                from = ui_elements[kv.Key].anchoredPosition;
                to = ui_elements[t].anchoredPosition;
                arr.anchoredPosition = (from + to) / 2f;
                arr.sizeDelta = new Vector2(64, 64);
                arr.right = from - to;
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

abstract class tech_tree_layout_engine
{
    protected List<technology> technologies;

    public tech_tree_layout_engine(IEnumerable<technology> technologies)
    {
        // Store technologies in increasing-dependence order
        this.technologies = new List<technology>(technologies);
        this.technologies.Sort((t1, t2) => t2.depends_on.Count.CompareTo(t1.depends_on.Count));
    }

    public abstract Dictionary<technology, float[]> evaluate_coordinates();
}

class swapper_layout_engine : tech_tree_layout_engine
{
    public swapper_layout_engine(IEnumerable<technology> technologies) : base(technologies) { }

    public override Dictionary<technology, float[]> evaluate_coordinates()
    {
        // Identify independent families of technologies
        var family_ids = new Dictionary<technology, int>();
        for (int i = 0; i < technologies.Count; ++i)
            family_ids[technologies[i]] = i;

        while (true)
        {
            bool families_updated = false;
            foreach (var kv in family_ids)
            {
                foreach (var kv2 in family_ids)
                    if (kv.Value != kv2.Value && kv.Key.linked_to(kv2.Key))
                    {
                        int new_fam = Mathf.Min(kv.Value, kv2.Value);
                        family_ids[kv.Key] = new_fam;
                        family_ids[kv2.Key] = new_fam;
                        families_updated = true;
                        break;
                    }

                if (families_updated)
                    break;
            }

            if (!families_updated)
                break;
        }

        // Construct family lists
        var families = new Dictionary<int, List<technology>>();
        foreach (var kv in family_ids)
        {
            if (!families.ContainsKey(kv.Value))
                families[kv.Value] = new List<technology>();
            families[kv.Value].Add(kv.Key);
        }

        // Construct the layout for each family
        var family_layouts = new Dictionary<int, family_layout>();
        foreach (var kv in families)
            family_layouts[kv.Key] = new family_layout(kv.Value);

        // Order families by size
        var family_order = new List<int>(families.Keys);
        family_order.Sort((a, b) => families[a].Count.CompareTo(families[b].Count));

        // Arrange each family next to each other
        Dictionary<technology, float[]> result = new Dictionary<technology, float[]>();
        int x_offset = 0;
        foreach (var family in family_order)
        {
            var layout = family_layouts[family];

            int min_x = int.MaxValue;
            int max_x = int.MinValue;
            foreach (var coord in layout.coords)
            {
                if (coord.Value[0] < min_x) min_x = coord.Value[0];
                if (coord.Value[0] > max_x) max_x = coord.Value[0];
            }

            foreach (var coord in layout.coords)
                result[coord.Key] = new float[] { coord.Value[0] + x_offset - min_x, coord.Value[1] };

            x_offset += 1 + max_x - min_x;
        }

        return result;
    }

    class family_layout
    {
        int grid_distance_heuristic(int x1, int x2) => (x1 - x2) * (x1 - x2);

        public Dictionary<technology, int[]> coords;

        public family_layout(List<technology> technologies)
        {
            // Load the technologies + init coordinates
            coords = new Dictionary<technology, int[]>();
            foreach (var t in technologies)
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

            // Work out initial columns by sorting each row by number of dependencies
            Dictionary<int, List<technology>> rows = new Dictionary<int, List<technology>>();
            foreach (var kv in coords)
            {
                if (!rows.ContainsKey(kv.Value[1]))
                    rows[kv.Value[1]] = new List<technology>();
                rows[kv.Value[1]].Add(kv.Key);
            }

            foreach (var kv in rows)
                kv.Value.Sort((t1, t2) => t2.depends_on.Count.CompareTo(t1.depends_on.Count));

            foreach (var kv in rows)
                for (int i = 0; i < kv.Value.Count; ++i)
                    coords[kv.Value[i]][0] = i;

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
                if (++iter > 10000)
                {
                    Debug.Log("Hit tech column iteration limit");
                    break;
                }

                bool swap_perfomed = false;

                for (int row = 0; row < matrix.GetLength(1); ++row)
                    for (int col = 1; col < matrix.GetLength(0); ++col)
                    {
                        technology left = matrix[col - 1, row];
                        technology right = matrix[col, row];

                        int score_unswapped = 0;
                        int score_swapped = 0;

                        // Add score for left technology before/after swap
                        if (left != null)
                        {
                            foreach (var c in children[left])
                            {
                                score_unswapped += grid_distance_heuristic(coords[c][0], (col - 1));
                                score_swapped += grid_distance_heuristic(coords[c][0], col);
                            }

                            foreach (var p in left.depends_on)
                            {
                                score_unswapped += grid_distance_heuristic(coords[p][0], (col - 1));
                                score_swapped += grid_distance_heuristic(coords[p][0], col);
                            }

                        }

                        // Add score for right technology before/after swap
                        if (right != null)
                        {
                            foreach (var c in children[right])
                            {
                                score_unswapped += grid_distance_heuristic(coords[c][0], col);
                                score_swapped += grid_distance_heuristic(coords[c][0], (col - 1));
                            }

                            foreach (var p in right.depends_on)
                            {
                                score_unswapped += grid_distance_heuristic(coords[p][0], col);
                                score_swapped += grid_distance_heuristic(coords[p][0], (col - 1));
                            }
                        }

                        if (score_swapped > score_unswapped)
                            continue; // Swap would be worse => stay as we are

                        if (score_swapped == score_unswapped)
                            if (iter % 2 == 0)
                                continue; // Swap would be same, swap sometimes

                        // Perform swap
                        matrix[col, row] = left;
                        if (left != null) coords[left][0] = col;

                        matrix[col - 1, row] = right;
                        if (right != null) coords[right][0] = col - 1;

                        swap_perfomed = true;
                    }

                // Converged
                if (!swap_perfomed)
                    break;
            }
        }
    }
}

class force_layout_engine : tech_tree_layout_engine
{
    public force_layout_engine(IEnumerable<technology> technologies) : base(technologies) { }

    bool should_merge(HashSet<technology> f1, HashSet<technology> f2)
    {
        foreach (var t1 in f1)
            foreach (var t2 in f2)
                if (t1.depends_on_set.Contains(t2) || t2.depends_on_set.Contains(t1))
                    return true;
        return false;
    }

    public override Dictionary<technology, float[]> evaluate_coordinates()
    {
        // Start with one family per technology
        var families = new List<HashSet<technology>>();
        foreach (var t in technologies)
            families.Add(new HashSet<technology> { t });

        // Merge families linked by dependency
        while (true)
        {
            bool merge_occurred = false;
            for (int i = 0; i < families.Count; ++i)
                for (int j = i + 1; j < families.Count; ++j)
                {
                    var fi = families[i];
                    var fj = families[j];

                    if (should_merge(fi, fj))
                    {
                        // Merge family j into family i. Because i < j,
                        // we only need to break out of the j loop.
                        fi.UnionWith(fj);
                        families.RemoveAt(j);
                        merge_occurred = true;
                        break;
                    }
                }

            if (!merge_occurred)
                break;
        }



        // Initilize the collection of nodes with well-seperated families
        var nodes = new List<node>();
        int sqrt_families = Mathf.CeilToInt(Mathf.Sqrt(families.Count));
        int max_family_size = 0;
        foreach (var f in families)
            max_family_size = Mathf.Max(max_family_size, f.Count);
        int sqrt_family_size = Mathf.CeilToInt(Mathf.Sqrt(max_family_size));

        for (int i = 0; i < families.Count; ++i)
        {
            int x_family = i % sqrt_families;
            int y_family = i / sqrt_families;

            x_family *= sqrt_family_size;
            y_family *= sqrt_family_size;

            foreach (var t in families[i])
                nodes.Add(new node
                {
                    technology = t,
                    x = x_family + Random.Range(-0.3f, 0.3f) * sqrt_family_size,
                    y = y_family + Random.Range(-0.3f, 0.3f) * sqrt_family_size
                });
        }

        // Carry out 1000 newton iterations
        for (int iter = 0; iter < 1000; ++iter)
        {
            // Work out forces on nodes
            foreach (var n in nodes)
                n.eval_force(nodes);

            // Make move according to forces
            foreach (var n in nodes)
            {
                n.x += n.force_x / 10f;
                n.y += n.force_y / 10f;
            }
        }

        // Normalize to resonable coordinate range
        float max_x = float.MinValue;
        float min_x = float.MaxValue;
        float max_y = float.MinValue;
        float min_y = float.MaxValue;
        foreach (var n in nodes)
        {
            if (n.x > max_x) max_x = n.x;
            if (n.x < min_x) min_x = n.x;
            if (n.y > max_y) max_y = n.y;
            if (n.y < min_y) min_y = n.y;
        }

        foreach (var n in nodes)
        {
            n.x = n.x - min_x;
            n.y = n.y - min_y;
        }

        // Return the result
        Dictionary<technology, float[]> result = new Dictionary<technology, float[]>();
        foreach (var n in nodes)
            result[n.technology] = new float[] { n.x, n.y };
        return result;
    }

    class node
    {
        public technology technology;
        public float x;
        public float y;

        public float force_x;
        public float force_y;

        public void eval_force(IEnumerable<node> nodes)
        {
            force_x = 0;
            force_y = 0;

            foreach (var n in nodes)
            {
                if (n.technology == technology)
                    continue; // No self-interaction

                // Add force from this node
                add_force(n);
            }

            // Add attractive interaction to origin
            force_x -= x;
            force_y -= y;
        }

        public void add_force(node other)
        {
            float dx = other.x - x;
            float dy = other.y - y;
            float r2 = dx * dx + dy * dy;

            if (r2 < 2f)
            {
                // Nodes too close => repulsive foce
                force_x += -dx / (r2 + 0.01f);
                force_y += -dy / (r2 + 0.01f);
                return;
            }

            if (linked_to(other))
            {
                // We are connected by dependance => attractive foce
                force_x += dx;
                force_y += dy;
                return;
            }

            // Not connected => repulsive
            force_x += -dx / (r2 + 0.01f);
            force_y += -dy / (r2 + 0.01f);
        }

        public bool linked_to(node other) =>
            other.technology.depends_on_set.Contains(technology) ||
            technology.depends_on_set.Contains(other.technology);
    }
}
