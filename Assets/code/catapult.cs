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

    private void Start()
    {
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
    }

    bool rotate_base_towards(Vector3 forward) => utils.rotate_towards(
        rotation_base,
        Quaternion.LookRotation(forward, rotation_base.up),
        base_rotation_speed * Time.deltaTime);

    void set_wind_up(float amount, out bool retracted, out bool fired)
    {
        firing_arm.localRotation = Quaternion.Lerp(
            firing_arm_fired.localRotation,
            firing_arm_reset.localRotation,
            Mathf.Clamp(amount, 0f, 1f)
        );

        retracted = amount >= 1f;
        fired = amount <= 0;
    }

    protected override bool stage_update(FIRE_STAGE stage, float progress)
    {
        switch (stage)
        {
            case FIRE_STAGE.SEARCHING_FOR_TARGET:

                // Reset to fired position + facing forward
                set_wind_up(1f - progress / prep_time, out bool retracted, out bool fired);
                rotate_base_towards(transform.forward);
                return false;

            case FIRE_STAGE.PREPARING_TO_FIRE:

                // Face towards target
                Vector3 target_forward = target.transform.position - rotation_base.position;
                target_forward.y = 0;
                target_forward.Normalize();
                var alligned = rotate_base_towards(target_forward);

                // Wind up
                set_wind_up(progress / prep_time, out retracted, out fired);

                // Add projectile when wound up
                if (retracted && projectile == null)
                    Debug.LogError("Projectile should create itself");

                return retracted && alligned;

            case FIRE_STAGE.FIRING:

                // Un-wind quickly
                set_wind_up(1 - progress / fire_time, out retracted, out fired);

                if (fired)
                {
                    // Loose projectile
                    var proj = projectile;
                    proj.target = target;
                    proj.transform.SetParent(null);
                }

                return fired;

            default:
                return true;
        }
    }
}
