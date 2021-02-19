using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class town_gate : portal, IAddsToInspectionText
{
    const float SLOW_UPDATE_TIME = 1f;
    const float MAX_APPROACH_DISTANCE = 30f;

    public settler_path_element path_element;
    public Transform outside_link;

    path enemy_approach_path;
    int bed_count;

    class town_gate_pathfinder : IPathingAgent
    {
        Transform outside_link;

        public town_gate_pathfinder(Transform outside_link)
        {
            this.outside_link = outside_link;
        }

        public Vector3 validate_position(Vector3 v, out bool valid)
        {
            if (v.y < world.SEA_LEVEL ||
                Vector3.Dot(v - outside_link.position, outside_link.forward) < 0)
            {
                valid = false;
                return v;
            }

            return pathfinding_utils.validate_walking_position(v, resolution, out valid);
        }

        public bool validate_move(Vector3 a, Vector3 b)
        {
            return pathfinding_utils.validate_walking_move(a, b,
                resolution, 1.5f, resolution / 2f);
        }

        public float resolution => 1f;
    }

    GameObject drawn_approach_path;
    bool draw_approach_path
    {
        get => drawn_approach_path != null;
        set
        {
            if (draw_approach_path == value)
                return; // No change

            if (drawn_approach_path != null) Destroy(drawn_approach_path);
            if (!value || enemy_approach_path == null) return;

            drawn_approach_path = new GameObject("approach_path");
            drawn_approach_path.transform.SetParent(transform);
            drawn_approach_path.transform.localPosition = Vector3.zero;

            for (int i = 1; i < enemy_approach_path.length; ++i)
            {
                Vector3 a = enemy_approach_path[i - 1];
                Vector3 b = enemy_approach_path[i];

                var link = Resources.Load<GameObject>("misc/path_link").inst();
                link.transform.SetParent(drawn_approach_path.transform);
                link.transform.position = (a + b) / 2 + Vector3.up * 0.5f;
                link.transform.forward = b - a;
                link.transform.localScale = new Vector3(0.1f, 0.1f, (b - a).magnitude);
            }
        }
    }

    void refresh_drawn_approach_path()
    {
        draw_approach_path = false;
        draw_approach_path = settler_path_element.draw_links;
    }

    private void Start()
    {
        // Don't spawn settlers if this is the 
        // equipped or blueprint version
        if (is_equpped || is_blueprint) return;

        update_gate_group(this);
        path_element.add_group_change_listener(() => update_gate_group(this));
        InvokeRepeating("slow_update", SLOW_UPDATE_TIME, SLOW_UPDATE_TIME);
    }

    private void Update()
    {
        if (is_equpped || is_blueprint) return;

        if (enemy_approach_path == null)
        {
            refresh_drawn_approach_path();
            var pfd = new town_gate_pathfinder(outside_link);
            Vector3 start = pfd.validate_position(outside_link.position, out bool valid);
            float target_distance = Mathf.Min(game.render_range, MAX_APPROACH_DISTANCE);
            random_path.success_func test = (v) => (v - start).magnitude > target_distance;
            enemy_approach_path = new random_path(start, test, test, pfd);
        }

        switch (enemy_approach_path.state)
        {
            case path.STATE.COMPLETE:
                if (enemy_approach_path.optimize(load_balancing.iter))
                {
                    // Path has been optimized, redraw approach path if needed
                    refresh_drawn_approach_path();
                }
                else
                {
                    // Path can't be optimized any more, ensure that it remains valid
                    if (!enemy_approach_path.validate(load_balancing.iter))
                        enemy_approach_path = null;
                }
                break;

            case path.STATE.SEARCHING:
                enemy_approach_path.pathfind(load_balancing.iter);
                break;

            case path.STATE.FAILED:
                enemy_approach_path = null;
                break;
        }

        draw_approach_path = settler_path_element.draw_links;
    }

    private void OnDestroy()
    {
        if (is_equpped) return;
        if (is_blueprint) return;
        unregister_gate(this);
    }

    private void OnDrawGizmos()
    {
        enemy_approach_path?.draw_gizmos();
    }

    void slow_update()
    {
        // Only spawn settlers on auth client
        if (!has_authority)
            return;

        var elements = settler_path_element.element_group(path_element.group);
        bed_count = 0;
        foreach (var e in elements)
            if (e.interactable is bed)
                bed_count += 1;

        if (settler.settler_count < bed_count)
            client.create(transform.position, "characters/settler");
    }

    public string added_inspection_text()
    {
        return "Beds     : " + bed_count + "\n" +
               "Settlers : " + settler.settler_count + "\n" +
               "Outside path length : " + enemy_approach_path?.length + " (" + enemy_approach_path?.state + ")";
    }

    public override string init_portal_name() { return "Town gate"; }
    protected override string portal_ui() { return "ui/town_gate"; }

    //#########//
    // ATTACKS //
    //#########//

    pinned_message attack_message;
    HashSet<character> under_attack_by = new HashSet<character>();

    public void trigger_attack(IEnumerable<string> characters_to_spawn)
    {
        Vector3 spawn_point = transform.position;
        if (enemy_approach_path != null && enemy_approach_path.state == path.STATE.COMPLETE)
            spawn_point = enemy_approach_path[enemy_approach_path.length - 1];

        foreach (var c in characters_to_spawn)
            if (Resources.Load<character>("characters/" + c) != null)
                client.create(spawn_point, "characters/" + c, parent: this);

        settler_task_assignment.on_attack_begin();
    }

    void update_attack_message()
    {
        if (attack_message != null)
            Destroy(attack_message);

        if (under_attack_by.Count == 0)
            return;

        Dictionary<string, int> attacker_counts = new Dictionary<string, int>();
        Dictionary<string, character> attacker_examples = new Dictionary<string, character>();
        foreach (var attacker in under_attack_by)
        {
            if (attacker_counts.ContainsKey(attacker.display_name))
                attacker_counts[attacker.display_name] += 1;
            else attacker_counts[attacker.display_name] = 1;
            attacker_examples[attacker.display_name] = attacker;
        }

        string message = "Under attack!\n(";
        foreach (var kv in attacker_counts)
            message += kv.Value + " " + (kv.Value > 1 ? attacker_examples[kv.Key].plural_name : kv.Key) + " ";
        message = message.Trim();
        message += ")";

        attack_message = gameObject.add_pinned_message(message, Color.red);
    }

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);

        if (child is character)
        {
            // Characters attacking this gate
            var c = (character)child;
            c.controller = new town_approach_controller(this);
            under_attack_by.Add(c);
            update_attack_message();
        }
    }

    public override void on_delete_networked_child(networked child)
    {
        base.on_delete_networked_child(child);

        if (child is character)
        {
            var c = (character)child;
            under_attack_by.Remove(c);
            update_attack_message();
        }
    }

    class town_approach_controller : ICharacterController
    {
        town_gate gate;
        int index = 0;

        public town_approach_controller(town_gate gate)
        {
            this.gate = gate;
        }

        public void control(character c)
        {
            if (gate == null)
            {
                c.delete();
                return;
            }

            if (gate.enemy_approach_path == null ||
                index >= gate.enemy_approach_path.length)
            {
                // Finished walking the path, or there wasn't a path to walk
                // begin the attack
                c.controller = new attack_controller(gate);
                return;
            }

            // Make my way into town along the approach path
            if (utils.move_towards_and_look(c.transform, gate.enemy_approach_path[
                gate.enemy_approach_path.length - 1 - index],
                Time.deltaTime * c.run_speed, 0.25f))
                index += 1;
        }

        public void on_end_control(character c) { }
        public void draw_gizmos() { }
        public void draw_inspector_gui() { }
        public string inspect_info() { return "Approaching town."; }
    }

    class attack_controller : ICharacterController
    {
        settler_path_element.path path;
        float local_speed_mod = 1.0f;
        town_gate gate;

        public attack_controller(town_gate gate)
        {
            this.gate = gate;
            local_speed_mod = Random.Range(0.9f, 1.1f);
        }

        public void control(character c)
        {
            if (gate == null)
            {
                c.delete();
                return;
            }

            // Walk a path if we have one
            if (path != null)
            {
                if (path.walk(c.transform, c.run_speed * local_speed_mod))
                    path = null;
                return;
            }

            // Get the current element that I'm at
            var current_element = settler_path_element.nearest_element(c.transform.position, gate.path_element.group);

            // Find the nearest (to within some random noize) target in the same group as me
            var target = settler.find_to_min((s) =>
            {
                if (s.group != current_element.group) return Mathf.Infinity;
                return (s.transform.position - c.transform.position).magnitude + Random.Range(0, 5f);
            });

            var target_element = settler_path_element.nearest_element(target.transform.position);

            if (target_element == current_element)
            {
                // Fight each other
                c.controller = new melee_fight_controller(c, target, c.controller);
                target.controller = new melee_fight_controller(target, c, target.controller);
                return;
            }

            path = new settler_path_element.path(current_element, target_element);
        }

        public void on_end_control(character c) { }

        public string inspect_info()
        {
            return "Searching for targets";
        }

        public void draw_gizmos()
        {
            path?.draw_gizmos(Color.red);
        }

        public void draw_inspector_gui() { }
    }

    class melee_fight_controller : ICharacterController
    {
        Vector3 fight_centre;
        Vector3 fight_axis;

        character fighting;
        ICharacterController return_control_to;
        bool complete = false;
        float timer = 0;

        List<arm> arms = new List<arm>();
        List<Transform> arm_targets = new List<Transform>();
        List<Vector3> arm_initial_pos = new List<Vector3>();

        public melee_fight_controller(character c, character fighting, ICharacterController return_control_to = null)
        {
            this.fighting = fighting;
            this.return_control_to = return_control_to;

            if (fighting == null)
            {
                complete = true;
                return;
            }

            Vector3 disp = fighting.transform.position - c.transform.position;
            if (disp.magnitude > Mathf.Max(c.melee_range, fighting.melee_range))
            {
                complete = true;
                return;
            }

            fight_centre = (c.transform.position + fighting.transform.position) / 2f;
            fight_axis = disp.normalized;

            Vector3 forward = fight_axis;
            forward.y = 0;
            c.transform.forward = forward;

            c.transform.position = fight_centre - fight_axis * 0.5f * c.melee_range;

            foreach (var arm in c.GetComponentsInChildren<arm>())
            {
                var target = new GameObject("arm_target").transform;
                arm.to_grab = target;
                target.position = arm.shoulder.position + fight_axis * arm.total_length / 2f;
                target.forward = c.transform.forward;
                arm_initial_pos.Add(target.position);
                arm_targets.Add(target);
                arms.Add(arm);
            }
        }

        public void on_end_control(character c)
        {
            foreach (var t in arm_targets)
                if (t != null)
                    Destroy(t.gameObject);

            complete = true;
        }

        public void control(character c)
        {
            if (fighting == null) // Enemy has died
                complete = true;

            if (complete)
            {
                // Return control to whatever had control before
                c.controller = return_control_to;
                return;
            }

            // Make the target fight back if not already fighting
            if (!(fighting.controller is melee_fight_controller))
                fighting.controller = new melee_fight_controller(fighting, c);

            // Run melee cooldown
            timer += Time.deltaTime * 1f;
            if (timer > c.melee_cooldown)
            {
                fighting.take_damage(c.melee_damage);
                timer = 0;
            }

            // Apply fight animation
            float cos = Mathf.Pow(Mathf.Cos(Mathf.PI * timer / c.melee_cooldown), 10);
            float sin = Mathf.Pow(Mathf.Sin(Mathf.PI * timer / c.melee_cooldown), 10);

            c.transform.position = fight_centre +
                fight_axis * 0.5f * c.melee_range * (0.2f * Mathf.Max(cos, sin) - 1f);

            for (int i = 0; i < arm_targets.Count; ++i)
            {
                var arm = arms[i];
                var targ = arm_targets[i];
                var init = arm_initial_pos[i];
                var final = fighting.transform.position + fighting.height * Vector3.up * 0.75f;
                final = arm.nearest_in_reach(final);

                float amt = i % 2 == 0 ? sin : cos;
                targ.position = Vector3.Lerp(init, final, amt);
            }
        }

        public string inspect_info()
        {
            return "Figting " + fighting.display_name;
        }

        public void draw_gizmos() { }
        public void draw_inspector_gui() { }
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static Dictionary<int, HashSet<town_gate>> town_gates_by_group;

    public static void initialize()
    {
        town_gates_by_group = new Dictionary<int, HashSet<town_gate>>();
    }

    public static HashSet<town_gate> gate_group(int group)
    {
        if (town_gates_by_group.TryGetValue(group, out HashSet<town_gate> set))
            return set;
        return new HashSet<town_gate>();
    }

    public static town_gate nearest_gate(Vector3 pos)
    {
        float min_dis = Mathf.Infinity;
        town_gate ret = null;

        foreach (var gg in town_gates_by_group)
            foreach (var g in gg.Value)
            {
                float dis = (g.transform.position - pos).sqrMagnitude;
                if (dis < min_dis)
                {
                    min_dis = dis;
                    ret = g;
                }
            }

        return ret;
    }

    public static bool group_under_attack(int group)
    {
        foreach (var g in gate_group(group))
            if (g.under_attack_by.Count > 0)
                return true;
        return false;
    }

    public delegate bool attacker_delegate(character c);
    public static void iterate_over_attackers(int group, attacker_delegate del)
    {
        foreach (var gate in gate_group(group))
            foreach (var c in gate.under_attack_by)
                if (del(c))
                    return;
    }

    static void update_gate_group(town_gate g)
    {
        unregister_gate(g);

        int group = g.path_element.group;
        if (town_gates_by_group.TryGetValue(group, out HashSet<town_gate> set))
            set.Add(g);
        else
            town_gates_by_group[group] = new HashSet<town_gate> { g };
    }

    static void unregister_gate(town_gate g)
    {
        foreach (var kv in town_gates_by_group)
            kv.Value.Remove(g);
    }
}