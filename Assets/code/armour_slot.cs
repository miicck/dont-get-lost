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

    UnityEngine.UI.Image background;

    public override void update(item item, int count, inventory inventory)
    {
        base.update(item, count, inventory);

        // Create an image to obscure the equipment schematic
        if (background == null)
        {
            background = Resources.Load<UnityEngine.UI.Image>("ui/armour_slot_background").inst(item_image.transform.parent);
            background.transform.SetAsFirstSibling();
            background.transform.position = item_image.transform.position;
        }
        background.enabled = item != null && count > 0;

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

/// <summary> An object that can equip armour. These objects are essentially
/// thought of as a collection of <see cref="armour_locator"/>s. </summary>
public interface ICanEquipArmour
{
    armour_locator[] armour_locators();
    float armour_scale();
    bool armour_visible(armour_piece.LOCATION location);
    Color hair_color();
}


/// <summary> Extension methods relating to armour. In particular,
/// relating to <see cref="ICanEquipArmour"/>. </summary>
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
            {
                al.equipped = armour;
                al.equipped?.on_equip(entity);
                if (!entity.armour_visible(al.location))
                    foreach (var r in al.equipped.GetComponentsInChildren<Renderer>())
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
    }
}