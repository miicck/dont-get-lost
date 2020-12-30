using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class guard_spot : settler_interactable, IAddsToInspectionText
{
    character target;
    float attack_timer = 0;

    bool in_range(character c)
    {
        return (c.transform.position - transform.position).magnitude < 20f;
    }

    public override void on_assign(settler s)
    {
        target = null;
        attack_timer = 0;
    }

    class projectile : MonoBehaviour
    {
        character target;

        public static projectile create(Vector3 start_pos, character target, float max_time = 2f)
        {
            var p = temporary_object.create(max_time).gameObject.AddComponent<projectile>();
            p.name = "projectile";
            p.target = target;
            p.transform.position = start_pos;

            var proj = Resources.Load<GameObject>("particle_systems/guard_spot_projectile").inst(p.transform.position);
            proj.transform.SetParent(p.transform);

            return p;
        }

        private void Update()
        {
            if (this == null || target == null)
                return;

            if (utils.move_towards(transform, target.projectile_target.position, Time.deltaTime * 20f))
            {
                target.take_damage(10);
                Destroy(gameObject);
            }
        }
    }

    public override void on_interact(settler s)
    {
        if (target == null || !in_range(target))
        {
            // Identify a new target
            town_gate.iterate_over_attackers(s.group, (c) =>
            {
                if (in_range(c))
                {
                    target = c;
                    return true;
                }
                return false;
            });

            return;
        }

        // We have an in-range target, attack it
        attack_timer += Time.deltaTime;
        if (attack_timer > 1f)
        {
            attack_timer = 0f;
            projectile.create(s.transform.position + Vector3.up * s.height.value * 1.5f, target);
        }

        s.transform.position = transform.position + Mathf.Sin(attack_timer * Mathf.PI) * transform.forward * 0.1f;
    }

    public override bool is_complete(settler s)
    {
        // Guard until attack is over
        return !town_gate.group_under_attack(s.group);
    }

    public override float move_to_speed(settler s)
    {
        // Run to the guard spot
        return s.run_speed;
    }

    public string added_inspection_text()
    {
        var gg = town_gate.gate_group(path_element.group);
        if (gg.Count == 0) return "No connected gates!";
        return gg.Count + " connected " + (gg.Count > 1 ? "gates" : "gate");
    }
}