using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class town_gate : building_material, IAddsToInspectionText
{
    const float SLOW_UPDATE_TIME = 1f;

    public List<gate> gates = new List<gate>();
    public settler_path_element path_element;

    int bed_count;

    private void Start()
    {
        // Don't spawn settlers if this is the 
        // equipped or blueprint version
        if (is_equpped) return;
        if (is_blueprint) return;
        InvokeRepeating("slow_update", SLOW_UPDATE_TIME, SLOW_UPDATE_TIME);
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
               "Settlers : " + settler.settler_count;
    }

    //#########//
    // ATTACKS //
    //#########//

    pinned_message attack_message;
    HashSet<character> under_attack_by = new HashSet<character>();

    public void trigger_attack(IEnumerable<string> characters_to_spawn)
    {
        foreach (var c in characters_to_spawn)
            if (Resources.Load<character>("characters/" + c) != null)
                client.create(transform.position, "characters/" + c, parent: this);
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
            c.controller = new attack_controller();
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

    class attack_controller : ICharacterController
    {
        settler_path_element.path path;
        float local_speed_mod = 1.0f;


        public attack_controller()
        {
            local_speed_mod = Random.Range(0.9f, 1.1f);
        }

        public void control(character c)
        {
            // Walk a path if we have one
            if (path != null)
            {
                if (path.walk(c.transform, c.run_speed * local_speed_mod))
                    path = null;
                return;
            }

            // Get the current element that I'm at
            var current_element = settler_path_element.nearest_element(c.transform.position);

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
}