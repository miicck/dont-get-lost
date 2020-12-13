using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class rifle : equip_in_hand
{
    public Transform butt;
    public Transform sight;
    public Transform grip;

    Transform left_hand_location;

    public int per_shot_damage = 10;
    public float field_of_view_multiplier = 1f;
    public float kick_angle_x = 20f;
    public float kick_angle_y = 10f;
    public float kick_recover_rate = 90f;
    public float max_reload_angle = 90f;
    public float reload_time = 1f;
    public float pump_reload_amplitude = 0f;
    public float fire_delay = 0f;

    arm right_arm;
    arm left_arm;

    float reset_field_of_view;
    Vector2 acc_kick_angle;
    float reloading_left;
    bool reloading => reloading_left > 10e-4;

    player player => GetComponentInParent<player>();

    public override void on_equip(bool local_player)
    {
        base.on_equip(local_player);

        left_hand_location = new GameObject("left_hand_location").transform;
        left_hand_location.SetParent(grip.transform);
        left_hand_location.localPosition = Vector3.zero;
        left_hand_location.localRotation = Quaternion.identity;

        foreach (var a in player.GetComponentsInChildren<arm>())
        {
            if (a.name.Contains("right")) right_arm = a;
            else left_arm = a;
        }

        left_arm.to_grab = left_hand_location;
        right_arm.to_grab = transform;
        if (player.camera != null)
            reset_field_of_view = player.camera.fieldOfView;

        locate();
    }

    public override void on_unequip(bool local_player)
    {
        base.on_unequip(local_player);
        if (player.camera != null)
            player.camera.fieldOfView = reset_field_of_view;
    }

    private void Update()
    {
        locate();
    }

    public override use_result on_use_start(player.USE_TYPE use_type)
    {
        if (use_type == player.USE_TYPE.USING_LEFT_CLICK)
            if (!reloading)
            {
                // Play sounds immediately, but potentially delay fire
                foreach (var ad in GetComponentsInChildren<AudioSource>())
                    ad.Play();

                if (fire_delay < 10e-4) fire();
                else Invoke("fire", fire_delay);
            }

        return use_result.complete;
    }

    class particle_player : MonoBehaviour
    {
        ParticleSystem system;
        float start_time = 0;

        public static particle_player create_and_play(ParticleSystem to_copy)
        {
            var ret = new GameObject("particle_player").AddComponent<particle_player>();
            ret.transform.position = to_copy.transform.position;
            ret.transform.rotation = to_copy.transform.rotation;

            ret.system = to_copy.inst();
            ret.system.transform.SetParent(ret.transform);
            ret.system.transform.localPosition = Vector3.zero;
            ret.system.transform.localRotation = Quaternion.identity;
            ret.system.Play();

            ret.start_time = Time.realtimeSinceStartup;

            return ret;
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup - start_time > 1f && system.particleCount == 0)
                Destroy(gameObject);
        }
    }

    void fire()
    {
        if (player.camera != null)
        {
            var to_damage = utils.raycast_for_closest<IAcceptsDamage>(player.camera_ray(), out RaycastHit hit);
            to_damage?.take_damage(per_shot_damage);

            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
                particle_player.create_and_play(ps);
        }

        acc_kick_angle.x += kick_angle_x;
        acc_kick_angle.y += kick_angle_y;
        player.mod_x_rotation(-kick_angle_x);
        player.mod_y_rotation(kick_angle_y);

        reload();
    }

    void reload()
    {
        reloading_left = 1;
    }

    float reloading_angle()
    {
        const float ROTATE_PERIOD = 0.2f;
        const float UNROTATE_PERIOD = 0.2f;

        if (reloading_left > 1f - ROTATE_PERIOD)
            return max_reload_angle * (1 - reloading_left) / ROTATE_PERIOD;

        else if (reloading_left < UNROTATE_PERIOD)
            return max_reload_angle * reloading_left / UNROTATE_PERIOD;

        return max_reload_angle;
    }

    void locate()
    {
        // Recover from kick
        Vector2 recover_angle = new Vector2(
            Mathf.Min(Time.deltaTime * kick_recover_rate, acc_kick_angle.x),
            Mathf.Min(Time.deltaTime * kick_recover_rate, acc_kick_angle.y)
        );
        acc_kick_angle -= recover_angle;
        player.mod_x_rotation(recover_angle.x);
        player.mod_y_rotation(-recover_angle.y);

        // Allign rifle but with shoulder
        transform.forward = player.eye_transform.forward;
        Vector3 delta = right_arm.shoulder.transform.position - butt.transform.position;
        transform.position += delta;

        // Apply reloading modification
        transform.Rotate(0, -reloading_angle(), 0);
        reloading_left -= Mathf.Min(reloading_left, Time.deltaTime / reload_time);
        left_hand_location.position = grip.position -
            pump_reload_amplitude * grip.forward * Mathf.Sin(reloading_left * Mathf.PI * 2);

        // Control the camera position if we're not in third person
        if (player.camera != null && player.first_person)
            set_camera_position();
    }

    void set_camera_position()
    {
        // Default to crosshair cursor
        player.cursor_sprite = "crosshair";

        if (controls.mouse_down(controls.MOUSE_BUTTON.RIGHT) && !reloading)
        {
            // ADS - lerp camera to sight position / sight field of view
            player.camera.fieldOfView = Mathf.Lerp(player.camera.fieldOfView,
                reset_field_of_view * field_of_view_multiplier, 10 * Time.deltaTime);
            player.cursor_sprite = "transparent";
            player.camera.transform.position =
                Vector3.Lerp(player.camera.transform.position,
                sight.transform.position, 10f * Time.deltaTime);
        }
        else
        {
            // Reset camera position when not in ADS
            player.camera.transform.localPosition = Vector3.Lerp(
                player.camera.transform.localPosition, Vector3.zero, 10f * Time.deltaTime);
            player.camera.fieldOfView = Mathf.Lerp(player.camera.fieldOfView, reset_field_of_view, 10 * Time.deltaTime);
            player.camera.transform.localRotation = Quaternion.identity;
        }
    }
}
