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
    public int magazine_size = 1;
    public float fire_delay = 0f;
    public float reload_time = 1f;
    public float repeat_delay = 0f;
    public float field_of_view_multiplier = 1f;
    public float kick_angle_x = 20f;
    public float kick_angle_y = 10f;
    public float kick_recover_rate = 90f;
    public float max_reload_angle = 90f;
    public float pump_reload_amplitude = 0f;
    public string hip_fire_sprite = "crosshair";

    player player_using;
    arm right_arm;
    arm left_arm;

    int bullets_in_mag;
    float reset_field_of_view;
    Vector2 acc_kick_angle;
    float time_in_state = 0;

    bool ads_triggered
    {
        get => _ads_triggered;
        set
        {
            _ads_triggered = value;

            // Switch cursors depending on mode of use
            if (_ads_triggered) player_using.cursor_sprite = "transparent";
            else player_using.cursor_sprite = hip_fire_sprite;
        }
    }
    bool _ads_triggered = false;

    /// <summary> How much reloading is left in the range [0,1] </summary>
    float reloading_left
    {
        get
        {
            if (state != STATE.RELOADING) return 0;
            return Mathf.Max((reload_time - time_in_state) / reload_time, 0);
        }
    }

    /// <summary> Possible states of shooting. </summary>
    enum STATE
    {
        READY,    // Ready to fire
        FIRING,   // In the process of firing (i.e flintlock rifle)
        FIRED,    // Fired a shot, preparing for next shot
        RELOADING // Reloading the rifle
    }

    /// <summary> The current state of shooting. </summary>
    STATE state
    {
        get => _state;
        set
        {
            if (_state == value)
                return;

            time_in_state = 0;
            _state = value;

            switch (_state)
            {
                case STATE.FIRING:
                    // Play sounds immediately, but potentially delay fire
                    foreach (var ad in GetComponentsInChildren<AudioSource>())
                    {
                        // No 2D component for remote clients
                        if (!player_using.has_authority)
                            ad.spatialBlend = 1.0f;
                        ad.Play();

                    }
                    break;

                case STATE.FIRED:
                    // We've just fired a shot
                    if (player_using.has_authority)
                    {
                        // Only check shot hits on authoirity client
                        var to_damage = utils.raycast_for_closest<IAcceptsDamage>(player_using.camera_ray(), out RaycastHit hit);
                        if (to_damage != null && !to_damage.Equals(player_using))
                            to_damage.take_damage(per_shot_damage);
                    }

                    // Create particle effects
                    foreach (var ps in GetComponentsInChildren<ParticleSystem>())
                        particle_player.create_and_play(ps);

                    // Apply kick
                    acc_kick_angle.x += kick_angle_x;
                    acc_kick_angle.y += kick_angle_y;

                    if (player_using.has_authority)
                    {
                        // Only actually apply kick on authority client
                        player_using.mod_x_rotation(-kick_angle_x);
                        player_using.mod_y_rotation(kick_angle_y);
                    }
                    break;
            }
        }
    }
    STATE _state;

    public override void on_equip(player player)
    {
        base.on_equip(player);
        player_using = player;
        bullets_in_mag = magazine_size;

        state = STATE.READY;

        left_hand_location = new GameObject("left_hand_location").transform;
        left_hand_location.SetParent(grip.transform);
        left_hand_location.localPosition = Vector3.zero;
        left_hand_location.localRotation = Quaternion.identity;

        foreach (var a in player_using.GetComponentsInChildren<arm>())
        {
            if (a.name.Contains("right")) right_arm = a;
            else left_arm = a;
        }

        left_arm.to_grab = left_hand_location;
        right_arm.to_grab = transform;

        if (player_using.has_authority)
        {
            reset_field_of_view = player_using.camera.fieldOfView;
            player_using.cursor_sprite = hip_fire_sprite;
        }

        locate();
    }

    public override void on_unequip(player player)
    {
        base.on_unequip(player);
        if (player_using.has_authority)
            player_using.camera.fieldOfView = reset_field_of_view;
        player_using = null;
    }

    private void Update()
    {
        time_in_state += Time.deltaTime;

        switch (state)
        {
            case STATE.FIRING:
                if (time_in_state > fire_delay)
                    state = STATE.FIRED;
                break;

            case STATE.RELOADING:
                if (time_in_state > reload_time)
                {
                    bullets_in_mag = magazine_size;
                    state = STATE.READY;
                }
                break;

            case STATE.FIRED:
                if (time_in_state > repeat_delay)
                    if (--bullets_in_mag <= 0)
                        state = STATE.RELOADING;
                    else
                        state = STATE.FIRING;
                break;
        }

        locate();
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

        // Only apply kick if player has authority
        if (player_using.has_authority)
        {
            player_using.mod_x_rotation(recover_angle.x);
            player_using.mod_y_rotation(-recover_angle.y);
        }

        // Allign rifle but with shoulder
        transform.forward = player_using.eye_transform.forward;
        Vector3 delta = right_arm.shoulder.transform.position - butt.transform.position;
        transform.position += delta;

        // Apply reloading modification
        transform.Rotate(0, -reloading_angle(), 0);
        left_hand_location.position = grip.position -
            pump_reload_amplitude * grip.forward * Mathf.Sin(reloading_left * Mathf.PI * 2);

        // Control the camera position if we're not in third person
        if (player_using.has_authority && player_using.first_person)
            set_camera_position();
    }

    void set_camera_position()
    {
        if (ads_triggered && state != STATE.RELOADING)
        {
            // ADS - lerp camera to sight position / sight field of view
            player_using.camera.fieldOfView = Mathf.Lerp(player_using.camera.fieldOfView,
                reset_field_of_view * field_of_view_multiplier, 10 * Time.deltaTime);
            player_using.camera.transform.position =
                Vector3.Lerp(player_using.camera.transform.position,
                sight.transform.position, 10f * Time.deltaTime);
        }
        else
        {
            // Reset camera position when not in ADS
            player_using.camera.transform.localPosition = Vector3.Lerp(
                player_using.camera.transform.localPosition, Vector3.zero, 10f * Time.deltaTime);
            player_using.camera.fieldOfView = Mathf.Lerp(player_using.camera.fieldOfView, reset_field_of_view, 10 * Time.deltaTime);
            player_using.camera.transform.localRotation = Quaternion.identity;
        }
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

    //##########//
    // ITEM USE //
    //##########//

    player_interaction[] uses;
    public override player_interaction[] item_uses()
    {
        if (uses == null) uses = new player_interaction[]
        {
            new fire_interaction(this),
            new ads_interaction(this)
        };
        return uses;
    }

    class fire_interaction : networked_player_interaction
    {
        rifle rifle;
        public fire_interaction(rifle rifle) { this.rifle = rifle; }

        public override controls.BIND keybind => controls.BIND.USE_ITEM;
        public override bool allow_held => true;

        public override string context_tip()
        {
            return "fire";
        }

        public override bool start_networked_interaction(player player)
        {
            if (player != rifle.player_using)
                throw new System.Exception("Player using has changed!");

            if (rifle.state == STATE.READY)
                rifle.state = STATE.FIRING;

            return false;
        }

        public override bool continue_networked_interaction(player player)
        {
            // continue_interaction is defined to ensure that the player is using the
            // object for long enough to let other clients know (see the note in 
            // the on_change_old_new method of current_item_use in the player class)
            if (rifle.state == STATE.READY) return true;
            return false;
        }
    }

    class ads_interaction : player_interaction
    {
        rifle rifle;
        public ads_interaction(rifle rifle) { this.rifle = rifle; }

        public override controls.BIND keybind => controls.BIND.ALT_USE_ITEM;
        public override bool allow_held => true;
        public override bool simultaneous() { return true; }

        public override string context_tip()
        {
            return "aim down sights";
        }

        public override bool start_interaction(player player)
        {
            rifle.ads_triggered = true;
            return false;
        }

        public override bool continue_interaction(player player) { return !triggered(player); }

        public override void end_interaction(player player)
        {
            rifle.ads_triggered = false;
        }
    }
}
