using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class armour_slot : inventory_slot
{
    public armour_piece.LOCATION location;
    public armour_piece.HANDEDNESS handedness = armour_piece.HANDEDNESS.EITHER;

    public override bool accepts(string item_name)
    {
        var itm = Resources.Load<armour_piece>("items/" + item_name);
        if (itm == null) return false;
        if (itm.location != location) return false;
        return armour_piece.compatible_handedness(handedness, itm.handedness);
    }

    protected override void on_change()
    {
        if (item == null)
        {
            player.current.clear_armour(location, handedness);
            return;
        }

        var itm = Resources.Load<armour_piece>("items/" + item);
        player.current.set_armour(itm, handedness);
    }

    protected override Sprite empty_sprite()
    {
        return null;
    }
}