using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fishing_rod : equip_in_hand
{
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
        fishing_rod rod;
        fishing_float fishing_float;
        int stage = 0;
        float cast_timer = 0;
        float cast_power = 0;
        RectTransform cast_power_meter;
        Vector2 power_meter_init_size;

        Vector3 cast_position;

        public fish(fishing_rod rod) => this.rod = rod;
        public override string context_tip() => "cast " + rod.display_name;
        public override controls.BIND keybind => controls.BIND.USE_ITEM;
        public override bool allow_held => true;
        public override bool allows_movement() => true;
        public override bool allows_mouse_look() => true;

        public delegate bool stage_func(player player);
        stage_func[] stages;

        public override bool start_networked_interaction(player player)
        {
            // Reset stuff
            stage = 0;
            cast_timer = 0;
            cast_power = 0;
            power_meter_init_size = default;

            // Create the cast power meter
            cast_power_meter = Resources.Load<RectTransform>("ui/cast_power_meter").inst();
            cast_power_meter.transform.SetParent(game.canvas.transform);
            cast_power_meter.transform.localPosition = Vector3.zero;
            set_cast_power_meter(0f);

            // Setup the stages
            stages = new stage_func[] { charge_cast, uncharge_cast, cast, wait_for_strike, reel_in };

            return false;
        }

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

        bool charge_cast(player player)
        {
            // Charge the cast
            cast_timer += Time.deltaTime;
            update_cast_power(player);
            return !triggered(player);
        }

        bool uncharge_cast(player player)
        {
            // Uncharge the cast (quickly)
            cast_timer = Mathf.Max(0, cast_timer - Time.deltaTime * 10);
            update_cast_power(player);
            return cast_timer <= 0;
        }

        bool cast(player player)
        {
            // Create the float + initialize it's trajectory (on auth client)
            if (!player.has_authority) return true;
            fishing_float = client.create(rod.transform.position, "misc/fishing_float") as fishing_float;
            fishing_float.set_velocity(player.eye_transform.forward * 10 * cast_power);

            Destroy(cast_power_meter.gameObject);
            cast_position = player.transform.position;

            return true;
        }

        void attract_player(player player)
        {
            Vector3 delta = cast_position - player.transform.position;
            player.transform.position += delta * Time.deltaTime;

            Quaternion target_look = Quaternion.LookRotation(fishing_float.transform.position -
                player.eye_transform.position, Vector3.up);
            player.set_look_rotation(Quaternion.Lerp(player.eye_transform.rotation, target_look, Time.deltaTime * 5));
        }

        bool wait_for_strike(player player)
        {
            // Click will trigger the strike
            attract_player(player);
            return triggered(player);
        }

        bool reel_in(player player)
        {
            if (fishing_float == null) return true;

            attract_player(player);

            // Reel the float in
            Vector3 delta = rod.transform.position - fishing_float.transform.position;
            delta.y = 0;
            fishing_float.transform.position += delta.normalized * Time.deltaTime;
            return delta.magnitude < 0.2f;
        }

        public override bool continue_networked_interaction(player player)
        {
            // Cycle through the stages of the interaction
            if (stage >= stages.Length) return true;
            if (stages[stage](player)) ++stage;
            return false;
        }

        public override void end_networked_interaction(player player)
        {
            // Reset hand rotation
            player.hand_centre.localRotation = Quaternion.identity;

            // Delete ui objects
            if (cast_power_meter != null) Destroy(cast_power_meter.gameObject);

            fishing_float?.delete();
        }
    }
}