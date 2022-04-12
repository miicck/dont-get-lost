using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class guard_spot : character_walk_to_interactable
{
    character target;
    float attack_timer = 0;

    bool in_range(character c) => (c.transform.position - transform.position).magnitude < 20f;

    bool valid_target(character target, character defender)
    {
        if (target == null) return false;
        if (target.is_dead) return false;
        if (!in_range(target)) return false;
        return true;
    }

    public override string task_summary()
    {
        if (target != null) return "Defending the town from a " + target.display_name;
        return "Defending the town";
    }

    protected override bool ready_to_assign(character c) => group_info.under_attack(c.group);

    protected override void on_arrive(character c)
    {
        // Reset stuff
        target = null;
        attack_timer = 0;
    }

    protected override STAGE_RESULT on_interact_arrived(character c, int stage)
    {
        bool force_new_target = (Time.frameCount + c.network_id) % 60 == 0;

        if (!valid_target(target, c) || force_new_target)
        {
            target = null;

            List<character> attackers = new List<character>();

            group_info.iterate_over_attackers(c.group, (a) =>
            {
                attackers.Add(a);
                return false;
            });

            attackers.Sort((a, b) =>
                (c.transform.position - a.transform.position).sqrMagnitude.CompareTo(
                (c.transform.position - b.transform.position).sqrMagnitude));

            foreach (var attacker in attackers)
                if (valid_target(attacker, c))
                {
                    target = attacker;
                    break;
                }
        }
        else
        {
            // We have an in-range target, attack it
            attack_timer += Time.deltaTime;
            if (attack_timer > 1f)
            {
                attack_timer = 0f;
                int damage = (int)(10 * current_proficiency.total_multiplier);
                float height_scale = 1f;
                if (c is settler) height_scale *= (c as settler).height_scale.value;
                projectile.create(c.transform.position + Vector3.up * height_scale * 1.5f, target, damage: damage);
            }
            c.transform.position = transform.position + Mathf.Sin(attack_timer * Mathf.PI) * transform.forward * 0.1f;
        }

        // Continue defending whilst attack is underway
        if (group_info.under_attack(c.group))
            return STAGE_RESULT.STAGE_UNDERWAY;
        return STAGE_RESULT.TASK_COMPLETE;
    }

    public override float move_to_speed(character c) => c.run_speed;

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