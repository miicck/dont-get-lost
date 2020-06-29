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

    public override bool allow_right_click_held_down()
    {
        return true;
    }

    public override use_result on_use_start(player.USE_TYPE use_type)
    {
        switch(use_type)
        {
            case player.USE_TYPE.USING_LEFT_CLICK:
                fire();
                break;

            case player.USE_TYPE.USING_RIGHT_CLICK:
                pickup_ammo();
                break;
        }

        return use_result.complete;
    }
}