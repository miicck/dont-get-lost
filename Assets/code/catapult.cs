using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class catapult : siege_engine
{
    public Transform rotation_base;
    public Transform firing_arm;
    public Transform firing_arm_fired;
    public siege_engine_projectile projectile_prefab;
    public Transform projectile_position;

    public float prep_time = 1f;
    public float fire_time = 0.2f;
    public float base_rotation_speed = 30f;

    Transform firing_arm_reset;

    siege_engine_projectile projectile
    {
        get
        {
            if (projectile_position.transform.childCount > 0)
                return projectile_position.transform.GetChild(0).GetComponent<siege_engine_projectile>();

            var inst = projectile_prefab.inst();
            inst.transform.SetParent(projectile_position);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;

            return inst;
        }
    }

    protected override void Start()
    {
        base.Start();

        // Record initial position of firing arm as reset position
        firing_arm_reset = new GameObject("firing_arm_reset").transform;
        firing_arm_reset.SetParent(firing_arm.parent);
        firing_arm_reset.localPosition = firing_arm.localPosition;
        firing_arm_reset.localRotation = firing_arm.localRotation;

        // Wrap the rotation base in an object so that forward is sensibly defined
        var new_base = new GameObject("base_wrapper").transform;
        new_base.transform.SetParent(rotation_base.transform.parent);
        new_base.transform.localPosition = rotation_base.transform.localPosition;
        new_base.transform.rotation = transform.rotation;
        rotation_base.transform.SetParent(new_base);
        rotation_base = new_base;

        // Start ready-to-fire
        retraction_amount = 1f;
        if (projectile == null)
            Debug.LogError("Projectile is supposed to create itself!");
    }

    bool rotate_base_towards(Vector3 forward, float speed) => utils.rotate_towards(
        rotation_base,
        Quaternion.LookRotation(forward, rotation_base.up),
        base_rotation_speed * Time.deltaTime);

    float retraction_amount
    {
        get => _retraction_amount;
        set
        {
            _retraction_amount = Mathf.Clamp(value, 0, 1);

            firing_arm.localRotation = Quaternion.Lerp(
                firing_arm_fired.localRotation,
                firing_arm_reset.localRotation,
                _retraction_amount
            );
        }
    }
    float _retraction_amount;

    Vector3 forward_target
    {
        get
        {
            if (target == null || target.is_dead) return transform.forward;
            Vector3 ret = target.transform.position - transform.position;
            ret.y = 0;
            ret.Normalize();
            return ret;
        }
    }

    bool ready_to_fire =>
        retraction_amount >= 1f &&
        projectile != null &&
        rotate_base_towards(forward_target, 0f);

    bool firing = false;

    void prepare_to_fire()
    {
        retraction_amount += current_proficiency.total_multiplier * Time.deltaTime / prep_time;
        rotate_base_towards(forward_target, base_rotation_speed);
    }

    void fire()
    {
        retraction_amount -= Time.deltaTime / fire_time;

        if (retraction_amount <= 0)
        {
            firing = false;

            // Loose projectile
            var proj = projectile;
            proj.target = target;
            proj.damage = Mathf.FloorToInt(proj.damage * current_proficiency.total_multiplier);
            proj.transform.SetParent(null);
        }
    }

    protected override bool ready_to_assign(character c)
    {
        // We should do this if we need to reset the catapult, or if we're under attack
        return !ready_to_fire || group_info.under_attack(c.group);
    }

    public override float move_to_speed(character c)
    {
        return c.run_speed;
    }

    protected override STAGE_RESULT on_interact_arrived(character c, int stage)
    {
        if (firing)
        {
            fire();
            return STAGE_RESULT.STAGE_UNDERWAY;
        }

        // Find a new target
        if (group_info.under_attack(c.group) && (target == null || target.is_dead))
        {
            // Seach for nearest target
            target = group_info.closest_attacker(transform.position);
            return STAGE_RESULT.STAGE_UNDERWAY;
        }

        // Make ready to fire
        if (!ready_to_fire)
        {
            prepare_to_fire();
            return STAGE_RESULT.STAGE_UNDERWAY;
        }

        // Ready to fire, but no target => we're done
        if (target == null || target.is_dead)
        {
            return STAGE_RESULT.TASK_COMPLETE;
        }

        // Everything is ready for us to fire
        firing = true;
        return STAGE_RESULT.STAGE_UNDERWAY;
    }

    public override string added_inspection_text() =>
        base.added_inspection_text() + "\n" +
        "Ready to fire: " + ready_to_fire + "\n" +
        "Firing: " + firing;
}
