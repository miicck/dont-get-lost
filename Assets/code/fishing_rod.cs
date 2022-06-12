using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fishing_rod : equip_in_hand
{
    public Transform rod_end;

    public float max_cast_velocity = 20f;

    //##########//
    // ITEM USE //
    //##########//

    player_interaction[] uses;
    public override player_interaction[] item_uses()
    {
        if (uses == null) uses = new player_interaction[] { new fish(this) };
        return uses;
    }

    class fish : networked_player_interaction
    {
        const float STRIKE_ANGLE = 30;
        const float MAX_REEL_SPEED = 5f;

        fishing_rod rod;
        fishing_float fishing_float;
        Vector3 float_velocity;
        int stage = 0;
        float cast_timer = 0;
        float cast_power = 0;
        RectTransform cast_power_meter;
        RectTransform info_panel;
        Vector2 power_meter_init_size;
        bool strike_success = false;
        bool following_float = false;
        float pull_amount = 0;

        public fish(fishing_rod rod) => this.rod = rod;
        public override string context_tip() => "cast " + rod.display_name;
        public override controls.BIND keybind => controls.BIND.USE_ITEM;
        public override bool allow_held => true;
        public override bool allows_movement() => fishing_float == null;
        public override bool allows_mouse_look() => !following_float;

        public enum STAGE_RESULT
        {
            COMPLETE,
            UNDERWAY,
            FAILED
        }

        public delegate STAGE_RESULT stage_func(player player);
        stage_func[] stages;

        public override bool start_networked_interaction(player player)
        {
            // Don't do anything on non-auth clients
            if (!player.has_authority) return false;

            // Reset stuff
            stage = 0;
            cast_timer = 0;
            cast_power = 0;
            pull_amount = 0;
            strike_success = false;
            following_float = false;
            power_meter_init_size = default;

            // Create the cast power meter
            cast_power_meter = Resources.Load<RectTransform>("ui/cast_power_meter").inst();
            cast_power_meter.transform.SetParent(game.canvas.transform);
            cast_power_meter.transform.localPosition = Vector3.zero;
            set_cast_power_meter(0f);

            // Remove the cursor
            player.cursor_sprite = null;

            // Setup the stages
            stages = new stage_func[] {
                charge_cast,
                uncharge_cast,
                cast,
                wait_for_float_submerged,
                wait_for_float_floating,
                wait_for_strike,
                reel_in,
                obtain_products
            };

            return false;
        }

        public override bool continue_networked_interaction(player player)
        {
            // Don't do anything on non-auth client
            if (!player.has_authority) return false;

            // Cycle through the stages of the interaction
            if (stage >= stages.Length) return true;

            switch (stages[stage](player))
            {
                case STAGE_RESULT.UNDERWAY:
                    break;
                case STAGE_RESULT.COMPLETE:
                    ++stage;
                    break;
                case STAGE_RESULT.FAILED:
                    stage = stages.Length;
                    break;
                default:
                    Debug.LogError("Unkown fishing stage!");
                    stage = stages.Length;
                    break;
            }

            return false;
        }

        public override void end_networked_interaction(player player)
        {
            // Reset hand rotation
            player.hand_centre.localRotation = Quaternion.identity;

            // Delete objects
            if (cast_power_meter != null) Destroy(cast_power_meter.gameObject);
            if (fishing_float != null) Destroy(fishing_float.gameObject);
            if (info_panel != null) Destroy(info_panel.gameObject);
        }

        //####################//
        // INTERACTION STAGES //
        //####################//

        STAGE_RESULT charge_cast(player player)
        {
            // Charge the cast
            cast_timer += Time.deltaTime;
            update_cast_power(player);
            return triggered(player) ? STAGE_RESULT.UNDERWAY : STAGE_RESULT.COMPLETE;
        }

        STAGE_RESULT uncharge_cast(player player)
        {
            // Uncharge the cast (quickly)
            cast_timer = Mathf.Max(0, cast_timer - Time.deltaTime * 10);
            update_cast_power(player);
            return cast_timer > 0 ? STAGE_RESULT.UNDERWAY : STAGE_RESULT.COMPLETE;
        }

        STAGE_RESULT cast(player player)
        {
            // Create the float + initialize it's trajectory (on auth client)
            fishing_float = Resources.Load<fishing_float>("misc/fishing_float").inst(rod.rod_end.position);
            float_velocity = player.eye_transform.forward * rod.max_cast_velocity * cast_power;
            Destroy(cast_power_meter.gameObject);
            return STAGE_RESULT.COMPLETE;
        }

        STAGE_RESULT wait_for_float_submerged(player player)
        {
            float_kinematics(bob: false);

            // Cast location is invalid
            if (fishing_float.under_terrain)
                return STAGE_RESULT.FAILED;

            // Wait until the float is submerged
            if (fishing_float.submerged_amount > 0.4f)
            {
                var ps = Resources.Load<ParticleSystem>("particle_systems/ripples").inst();

                var emission = ps.emission;
                emission.enabled = false; // We will control emission

                ps.transform.position = fishing_float.transform.position;
                ps.transform.forward = Vector3.down;
                var ft = ps.gameObject.AddComponent<float_tracker>();
                ft.tracking = fishing_float;

                info_panel = Resources.Load<RectTransform>("ui/fishing_info").inst();
                info_panel.SetParent(game.canvas.transform);
                info_panel.localPosition = Vector3.zero;

                set_info();

                return STAGE_RESULT.COMPLETE;
            }
            return STAGE_RESULT.UNDERWAY;
        }

        STAGE_RESULT wait_for_float_floating(player player)
        {
            set_info();
            float_kinematics(bob: false);
            return fishing_float.submerged_amount < 0.5f ? STAGE_RESULT.COMPLETE : STAGE_RESULT.UNDERWAY;
        }

        STAGE_RESULT wait_for_strike(player player)
        {
            // Bob up and down
            set_info();
            float_kinematics();

            // Wait for a strike 
            follow_float(player);
            if (!triggered(player)) return STAGE_RESULT.UNDERWAY;

            if (fishing_float.strikeable)
            {
                // Successful strike
                strike_success = true;
            }

            return STAGE_RESULT.COMPLETE;
        }

        STAGE_RESULT reel_in(player player)
        {
            set_info();
            follow_float(player);

            // Reel the float in
            Vector3 towards_rod = rod.rod_end.position - fishing_float.transform.position;
            towards_rod.y = 0;

            // Modify the pull amount by pulling down with the mouse
            float pull = -Mathf.Min(0, controls.delta(controls.BIND.FISHING_ROD_PULL));
            pull_amount += (pull - 1) * Time.deltaTime;
            pull_amount = Mathf.Clamp(pull_amount, 0f, 5f);

            // Pulling animation
            float pull_strength = Mathf.Clamp(1f - Mathf.Exp(-pull_amount), 0, 0.99f);
            Vector3 up = player.transform.up * (1 - pull_strength) - player.transform.forward * pull_strength;
            Vector3 forward = player.transform.forward;
            forward -= Vector3.Project(forward, up);
            player.hand_centre.rotation = Quaternion.LookRotation(forward, up);

            if (strike_success)
            {
                // Pull to reel in
                float speed = (pull_strength - 0.5f) * 2f * MAX_REEL_SPEED;
                if (towards_rod.magnitude > 10 && speed < 0) speed = 0; // Don't go too far away
                fishing_float.transform.position += towards_rod.normalized * Time.deltaTime * speed;

                // Kicks from the fish
                fishing_float.transform.position += Random.onUnitSphere * Time.deltaTime;

                // Stay near the surface
                Vector3 pos = fishing_float.transform.position;
                pos.y = Mathf.Clamp(pos.y, world.SEA_LEVEL - 0.1f, world.SEA_LEVEL);
                fishing_float.transform.position = pos;
            }
            else
            {
                // Just reel in, sadly :(
                fishing_float.transform.position += towards_rod.normalized * Time.deltaTime * (1 + pull_strength) * 0.5f * MAX_REEL_SPEED;
                fishing_float.transform.position += Vector3.up * Mathf.Sin(Time.time * 10) * Time.deltaTime / 5f;
            }

            return towards_rod.magnitude < 1f || fishing_float.water_depth < 0.1f ? STAGE_RESULT.COMPLETE : STAGE_RESULT.UNDERWAY;
        }

        STAGE_RESULT obtain_products(player player)
        {
            if (!strike_success) return STAGE_RESULT.COMPLETE;
            player.inventory.add("cod", 1);
            return STAGE_RESULT.COMPLETE;
        }

        //########################################//
        // Utility functions, used in cast stages //
        //########################################//

        void set_cast_power_meter(float val)
        {
            // Update the ui to reflect the instantaneous cast power
            var green = cast_power_meter.Find("green").GetComponent<RectTransform>();
            if (power_meter_init_size == default) power_meter_init_size = green.sizeDelta;
            green.sizeDelta = new Vector2(power_meter_init_size.x * val, power_meter_init_size.y);
            cast_power_meter.GetComponentInChildren<UnityEngine.UI.Text>().text = "Cast power " + (int)(val * 100) + "%";
        }

        void update_cast_power(player player)
        {
            // Work out the instentaneous cast power (the cast power
            // is the maximum instantaneous cast power)
            float inst_cast_power = 1f - Mathf.Exp(-cast_timer);
            if (inst_cast_power > cast_power) cast_power = inst_cast_power;
            set_cast_power_meter(inst_cast_power);

            // Perform casting animation
            Vector3 up = player.transform.up * (1 - inst_cast_power) - player.transform.forward * inst_cast_power;
            Vector3 forward = player.transform.forward;
            forward -= Vector3.Project(forward, up);
            player.hand_centre.rotation = Quaternion.LookRotation(forward, up);
        }

        void follow_float(player player)
        {
            following_float = true;
            Vector3 toward_float = fishing_float.transform.position - player.eye_transform.position;
            Quaternion current_look = player.eye_transform.rotation;
            Quaternion target_look = Quaternion.LookRotation(toward_float, Vector3.up);
            player.set_look_rotation(Quaternion.Lerp(current_look, target_look, Time.deltaTime * 5f));
        }

        void float_kinematics(bool bob = true)
        {
            // Gravity/bouyancy
            float_velocity.y -= Time.deltaTime * (10 - fishing_float.submerged_amount * 20);

            // Damping
            float_velocity -= Time.deltaTime * float_velocity * fishing_float.submerged_amount * 10;

            // Random pulls downward
            if (bob && ((int)Time.time) % 10 == Random.Range(0, 10))
            {
                float mag = Random.Range(0.8f, 1.2f);
                float_velocity -= mag * Vector3.up;
            }

            // Apply velocity
            fishing_float.transform.position += float_velocity * Time.deltaTime;

            // Lean towards direction
            Vector3 up_mod = float_velocity;
            up_mod.y = 0;
            fishing_float.transform.up = Vector3.up + up_mod;
        }

        void set_info()
        {
            var t = info_panel.GetComponentInChildren<UnityEngine.UI.Text>();

            Vector3 to_rod = rod.rod_end.position - fishing_float.transform.position;
            to_rod.y = 0;

            t.text = "Depth    : " + fishing_float.water_depth.ToString("F1") + "M\n" +
                     "Distance : " + to_rod.magnitude.ToString("F1") + "M";
        }

        class float_tracker : MonoBehaviour
        {
            public fishing_float tracking;

            float last_y = world.SEA_LEVEL + 1;
            float last_emit_time = -1;

            void emit()
            {
                if (Time.time - last_emit_time < 0.2f) return;
                last_emit_time = Time.time;
                GetComponent<ParticleSystem>().Emit(1);
            }

            private void Update()
            {
                if (tracking == null)
                {
                    Destroy(gameObject);
                    return;
                }

                Vector3 pos = tracking.transform.position;
                if (pos.y < last_y) emit();
                last_y = pos.y;
                pos.y = world.SEA_LEVEL;
                transform.position = pos;
            }
        }
    }
}