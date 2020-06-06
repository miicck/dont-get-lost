using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(item_requirement))]
public class ranged_weapon : equip_in_hand
{
    item_requirement ammunition { get => GetComponent<item_requirement>(); }
    public float projectile_velocity = 10f;

    public override use_result on_use_start(player.USE_TYPE use_type)
    {
        item ammo_found = null;

        // Search for ammunition in the player inventory
        foreach (var kv in player.current.inventory.contents.contents())
        {
            item i = Resources.Load<item>("items/" + kv.Key);
            if (ammunition.satisfied(i))
            {
                ammo_found = i;
                break;
            }
        }

        if (ammo_found == null)
        {
            popup_message.create("No ammunition!");
            return use_result.complete;
        }

        player.current.inventory.contents.remove(ammo_found.name, 1);
        Vector3 fired_position = 
            player.current.hand_centre.position + 
            player.current.hand_centre.forward;

        var fired = client.create(
            fired_position, "items/" + ammo_found.name,
            rotation: player.current.camera.transform.rotation);

        var rb = fired.gameObject.AddComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.velocity = player.current.camera.transform.forward * projectile_velocity;

        return use_result.complete;
    }
}