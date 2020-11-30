using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICanEquipArmour
{
    armour_locator[] armour_locators();
    float armour_scale();
}

public static class armour_extensions
{
    public static void clear_armour(this ICanEquipArmour entity,
        armour_piece.LOCATION location, armour_piece.HANDEDNESS handedness)
    {
        foreach (var al in entity.armour_locators())
            if (al.location == location && al.handedness == handedness)
            {
                al.equipped = null;
                return;
            }

        string err = "Could not find armour_locator with the location ";
        err += location + " and handedness " + handedness;
        throw new System.Exception(err);
    }

    public static void set_armour(this ICanEquipArmour entity,
        armour_piece armour, armour_piece.HANDEDNESS handedness)
    {
        foreach (var al in entity.armour_locators())
            if (al.location == armour.location && al.handedness == handedness)
                al.equipped = armour;
    }
}

/// <summary> A special slot in an inventory for armour. </summary>
public class armour_slot : inventory_slot, IInspectable
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

        // Find the entity that this slot belongs to
        var entity = inventory.GetComponentInParent<ICanEquipArmour>();
        if (entity == null)
            throw new System.Exception("Armour slot doesn't belong to something that can equip armour!");

        if (item == null) entity.clear_armour(location, handedness);
        else entity.set_armour((armour_piece)item, handedness);
    }

    public override Sprite empty_sprite()
    {
        // Don't display anything when the armour
        // slot is unoccupied, so that we can see
        // the equipment background illustration.
        return null;
    }
}