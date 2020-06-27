using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A special slot in an inventory for armour. </summary>
public class armour_slot : inventory_slot
{
    /// <summary> The location on the body that this armour slot corresponds to. </summary>
    public armour_piece.LOCATION location;

    /// <summary> The side of the body that this armour slot corresponds to. </summary>
    public armour_piece.HANDEDNESS handedness = armour_piece.HANDEDNESS.EITHER;

    /// <summary> Returns true if this armour slot 
    /// accepts the item with the given name. </summary>
    public override bool accepts(item item)
    {
        if (!base.accepts(item)) return false;
        if (item is armour_piece)
        {
            var arm = (armour_piece)item;
            if (arm.location != location) return false;
            return armour_piece.compatible_handedness(handedness, arm.handedness);
        }
        return false;
    }

    delegate void update_func();
    update_func pending_update;

    public override void update(item item, int count, inventory inventory)
    {
        base.update(item, count, inventory);

        // Find the player that this slot belongs to
        player player = inventory.GetComponentInParent<player>();
        if (player == null)
            throw new System.Exception("Armour slot doesn't belong to a player!");

        // Update the player armour
        if (item == null) player.clear_armour(location, handedness);
        else  player.set_armour((armour_piece)item, handedness);
    }

    public override Sprite empty_sprite()
    {
        // Don't display anything when the armour
        // slot is unoccupied, so that we can see
        // the equipment background illustration.
        return null;
    }
}