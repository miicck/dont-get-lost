using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(item_requirement))]
public class ranged_weapon : equip_in_hand
{
    item_requirement ammunition { get => GetComponent<item_requirement>(); }
    public float projectile_velocity = 10f;

    void fire()
    {
        item ammo_found = null;

        // Search for ammunition in the player inventory
        foreach (var kv in player.current.inventory.contents())
            if (ammunition.satisfied(kv.Key))
            {
                ammo_found = kv.Key;
                break;
            }

        if (ammo_found == null)
        {
            popup_message.create("No ammunition!");
            return;
        }

        player.current.inventory.remove(ammo_found.name, 1);
        Vector3 fired_position =
            player.current.hand_centre.position +
            player.current.hand_centre.forward *
            Resources.Load<projectile>("items/" + ammo_found.name).start_distance;

        var fired = client.create(
            fired_position, "items/" + ammo_found.name,
            rotation: player.current.camera.transform.rotation);

        var rb = fired.gameObject.AddComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.velocity = player.current.camera.transform.forward * projectile_velocity;
    }

    void pickup_ammo()
    {
        var ray = player.current.camera_ray(player.INTERACTION_RANGE, out float distance);
        foreach (var hit in Physics.RaycastAll(ray, distance))
        {
            var proj = hit.transform.GetComponent<projectile>();
            if (proj != null) proj.pick_up();
        }
    }

    //############//
    // PLAYER USE //
    //############//

    player_interaction[] uses;
    public override player_interaction[] item_uses()
    {
        if (uses == null)
            uses = new player_interaction[]
            {
            new fire_interaction(this),
            new pickup_ammo_interaction(this)
        };
        return uses;
    }

    class fire_interaction : player_interaction
    {
        ranged_weapon weapon;
        public fire_interaction(ranged_weapon rw) { weapon = rw; }

        public override controls.BIND keybind => controls.BIND.USE_ITEM;

        public override string context_tip()
        {
            return "fire "+weapon.display_name;
        }

        protected override bool on_start_interaction(player player)
        {
            weapon.fire();
            return true;
        }
    }

    class pickup_ammo_interaction : player_interaction
    {
        ranged_weapon weapon;
        public pickup_ammo_interaction(ranged_weapon rw) { weapon = rw; }

        public override controls.BIND keybind => controls.BIND.ALT_USE_ITEM;
        public override bool allow_held => true;

        public override string context_tip()
        {
            return "pickup ammo";
        }

        public override bool continue_interaction(player player)
        {
            if (!controls.held(controls.BIND.ALT_USE_ITEM))
                return true;

            weapon.pickup_ammo();
            return false;
        }
    }
}