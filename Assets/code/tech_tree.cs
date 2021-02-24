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

    private void Start()
    {
        init_tech();
    }

    //##############//
    // STATIC STUFF //
    //##############//

    public static void run_solver()
    {
        technology.solver_iteration();
    }

    class technology
    {
        public string name { get; private set; }

        private technology(string name)
        {
            this.name = name;
        }

        public void add_dependence(params technology[] others)
        {
            foreach (var other in others)
            {
                if (other.name == this.name) continue; // Can't depend on myself
                depends_on.Add(other);
                other.prereq_for.Add(this);
            }
        }

        List<technology> depends_on = new List<technology>();
        List<technology> prereq_for = new List<technology>();

        public RectTransform ui_element
        {
            get => _ui_element;
            set
            {
                if (value != null)
                {
                    value.anchoredPosition = new Vector2(256, -256) + Random.insideUnitCircle;
                }
                _ui_element = value;
            }
        }
        RectTransform _ui_element;

        public Vector2 position
        {
            get => _ui_element.anchoredPosition;
            set => _ui_element.anchoredPosition = value;
        }

        List<RectTransform> arrows = new List<RectTransform>();

        void redraw_arrows()
        {
            if (arrows.Count < depends_on.Count)
            {
                foreach (var a in arrows) Destroy(a.gameObject);
                arrows = new List<RectTransform>();

                foreach (var t in depends_on)
                {
                    var arr = new GameObject("arrow").AddComponent<RectTransform>();
                    arr.anchorMin = new Vector2(0, 1);
                    arr.anchorMax = new Vector2(0, 1);
                    var img = arr.gameObject.AddComponent<UnityEngine.UI.Image>();
                    img.color = Color.black;
                    arr.SetParent(ui_element.parent);
                    arr.SetAsFirstSibling();
                    arrows.Add(arr);
                }
            }

            for (int i = 0; i < arrows.Count; ++i)
            {
                var t = depends_on[i];
                var a = arrows[i];
                a.anchoredPosition = (position + t.position) / 2f;
                a.sizeDelta = new Vector2(2, (t.position - position).magnitude);
                a.up = t.position - position;
            }
        }

        //##############//
        // STATIC STUFF //
        //##############//

        static Dictionary<string, technology> all = new Dictionary<string, technology>();

        public static technology create(string name)
        {
            if (all.TryGetValue(name, out technology t))
                return t;

            t = new technology(name);
            all[name] = t;
            return t;
        }

        public static HashSet<technology> get_all()
        {
            HashSet<technology> ret = new HashSet<technology>();
            foreach (var kv in all) ret.Add(kv.Value);
            return ret;
        }

        class layer_solver
        {
            Dictionary<int, List<technology>> rows = new Dictionary<int, List<technology>>();
            float max_force = 0f;

            public layer_solver(IEnumerable<technology> techs)
            {
                // Work out which row each technology goes into
                Dictionary<technology, int> t_to_row = new Dictionary<technology, int>();
                foreach (var t in techs) t_to_row[t] = 0;

                int iter = 0;
                while (true)
                {
                    if (++iter > 1000)
                    {
                        Debug.LogError("Could not identify technology rows in 1000 iterations, are cycles present?");
                        break;
                    }

                    bool changed = false;
                    foreach (var t in techs)
                    {
                        int row_t = t_to_row[t];
                        foreach (var td in t.depends_on)
                        {
                            int row_td = t_to_row[td];
                            if (row_td >= row_t)
                            {
                                changed = true;
                                row_t = t_to_row[t] = row_td + 1;
                            }
                        }
                    }
                    if (!changed) break;
                }

                // Put the technolgies into rows
                foreach (var kv in t_to_row)
                {
                    if (!rows.ContainsKey(kv.Value)) rows[kv.Value] = new List<technology>();
                    rows[kv.Value].Add(kv.Key);
                }

                for (int i = 0; i < rows.Count; ++i)
                    for (int j = 0; j < rows[i].Count; ++j)
                        rows[i][j].position = new Vector2(64 + j * 64, -64 - 64 * i);
            }

            float attractive_force(technology t, technology t2)
            {
                return (t2.position.x - t.position.x) / 64f;
            }

            float repulsive_force(technology t, technology t2)
            {
                float dx = t.position.x - t2.position.x;
                return Mathf.Sign(dx) * 64f / (1f + Mathf.Abs(dx));
            }

            float force(technology t, List<technology> row)
            {
                float f = 0;

                // Add repulsive force from all other technologies in the same row
                foreach (var t2 in row)
                {
                    if (t2 == t) continue;
                    f += repulsive_force(t, t2);
                }

                // Add attractive force to all dependent/prerequisite technologies
                foreach (var t2 in t.depends_on) f += attractive_force(t, t2);
                foreach (var t2 in t.prereq_for) f += attractive_force(t, t2);

                return f;
            }

            public void run_iter()
            {
                float max_force_last = max_force;
                max_force = 0;

                foreach (var kv in rows)
                {
                    var row = kv.Value;
                    foreach (var t in row)
                    {
                        var f = force(t, row);
                        t.position += Vector2.right * (max_force + 1) * f;
                        if (Mathf.Abs(f) > max_force) max_force = Mathf.Abs(f);
                    }
                }

                foreach (var kv in all)
                {
                    var t = kv.Value;
                    t.redraw_arrows();
                }
            }
        }
        static layer_solver layers;

        static void solver_iteration_layers()
        {
            if (layers == null)
                layers = new layer_solver(get_all());

            layers.run_iter();
        }

        public static void solver_iteration()
        {
            solver_iteration_layers();
        }
    }

    static void init_tech()
    {
        var a_tech = technology.create("a");
        var b_tech = technology.create("b");
        var c_tech = technology.create("c");
        var d_tech = technology.create("d");
        var e_tech = technology.create("e");
        var f_tech = technology.create("f");
        var g_tech = technology.create("g");
        var h_tech = technology.create("h");
        var i_tech = technology.create("i");

        b_tech.add_dependence(a_tech);
        c_tech.add_dependence(a_tech);
        d_tech.add_dependence(b_tech, c_tech);
        e_tech.add_dependence(f_tech, d_tech);
        g_tech.add_dependence(e_tech);
        h_tech.add_dependence(e_tech);
        i_tech.add_dependence(e_tech);
    }

    public static RectTransform generate_tech_tree()
    {
        var ui = Resources.Load<RectTransform>("ui/tech_tree").inst();
        var content = ui.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;

        var tech_template = content.GetChild(0);

        int i = 0;
        foreach (var t in technology.get_all())
        {
            var tt = tech_template.inst().GetComponent<RectTransform>();
            tt.SetParent(tech_template.parent);
            tt.GetComponentInChildren<UnityEngine.UI.Text>().text = t.name;
            tt.name = t.name;
            t.ui_element = tt;
            i += 1;
        }

        tech_template.SetParent(null);
        Destroy(tech_template.gameObject);

        ui.SetParent(game.canvas.transform);
        ui.anchoredPosition = Vector3.zero;
        return ui;
    }
}