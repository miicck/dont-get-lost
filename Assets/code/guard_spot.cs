using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class guard_spot : walk_to_settler_interactable
{
    character target;
    float attack_timer = 0;

    bool in_range(character c) => (c.transform.position - transform.position).magnitude < 20f;
    bool valid_target(character c) => c != null && !c.is_dead && in_range(c);

    public override string task_summary()
    {
        if (target != null) return "Defending the town from a " + target.display_name;
        return "Defending the town";
    }

    protected override bool ready_to_assign(settler s)
    {
        // Only need to defend if under attack
        return group_info.under_attack(s.group);
    }

    protected override void on_arrive(settler s)
    {
        // Reset stuff
        target = null;
        attack_timer = 0;
    }

    protected override STAGE_RESULT on_interact_arrived(settler s, int stage)
    {
        if (!valid_target(target))
        {
            // Identify a new target
            group_info.iterate_over_attackers(s.group, (c) =>
            {
                if (valid_target(c))
                {
                    target = c;
                    return true;
                }
                return false;
            });
        }
        else
        {
            // We have an in-range target, attack it
            attack_timer += Time.deltaTime;
            if (attack_timer > 1f)
            {
                attack_timer = 0f;
                int damage = (int)(10 * current_proficiency.total_multiplier);
                projectile.create(s.transform.position + Vector3.up * s.height_scale.value * 1.5f, target, damage: damage);
            }
            s.transform.position = transform.position + Mathf.Sin(attack_timer * Mathf.PI) * transform.forward * 0.1f;
        }

        // Continue defending whilst attack is underway
        if (group_info.under_attack(s.group))
            return STAGE_RESULT.STAGE_UNDERWAY;
        return STAGE_RESULT.TASK_COMPLETE;
    }

    public override float move_to_speed(settler s)
    {
        // Run to the guard spot
        return s.run_speed;
    }

    class projectile : MonoBehaviour
    {
        character target;
        int damage;

        public static projectile create(Vector3 start_pos, character target, float max_time = 2f, int damage = 10)
        {
            var p = temporary_object.create(max_time).gameObject.AddComponent<projectile>();
            p.name = "projectile";
            p.target = target;
            p.damage = damage;
            p.transform.position = start_pos;

            var proj = Resources.Load<GameObject>("particle_systems/guard_spot_projectile").inst(p.transform.position);
            proj.transform.SetParent(p.transform);

            return p;
        }

        private void Update()
        {
            if (this == null || target == null)
                return;

            if (utils.move_towards(transform, target.projectile_target(), Time.deltaTime * 20f))
            {
                target.take_damage(damage);
                Destroy(gameObject);
            }
        }
    }
}