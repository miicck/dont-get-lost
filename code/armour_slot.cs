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

    /// <summary> The player that this armour slot belongs to. Will attempt
    /// to identify that player if not yet identified. </summary>
    player belongs_to
    {
        get
        {
            if (_belongs_to == null)
                foreach (var sec in sections_belonging_to)
                    if (sec.belongs_to != null)
                    {
                        _belongs_to = sec.belongs_to;
                        break;
                    }

            return _belongs_to;
        }
    }
    player _belongs_to;

    /// <summary> Returns true if this armour slot 
    /// accepts the item with the given name. </summary>
    public override bool accepts(string item_name)
    {
        var itm = Resources.Load<armour_piece>("items/" + item_name);
        if (itm == null) return false;
        if (itm.location != location) return false;
        return armour_piece.compatible_handedness(handedness, itm.handedness);
    }

    delegate void update_func();
    update_func pending_update;

    protected override void on_change()
    {
        // When the item changes, we queue an update
        // to the players armour. We cannot do this
        // immediately, because belongs_to might not 
        // yet have been set (for example when the 
        // network loads the inventory for the first time).
        if (item == null)
        {
            pending_update = () =>
            {
                belongs_to.clear_armour(location, handedness);
            };
            InvokeRepeating("run_pending", 0.1f, 0.1f);
            return;
        }

        pending_update = () =>
        {
            var itm = Resources.Load<armour_piece>("items/" + item);
            belongs_to.set_armour(itm, handedness);
        };
        InvokeRepeating("run_pending", 0.1f, 0.1f);
    }

    /// <summary> Run pending armour updates. </summary>
    void run_pending()
    {
        if (pending_update == null) return;
        if (belongs_to != null)
        {
            // Player has been found, run the armour update
            // and cancel the pending updates.
            pending_update();
            pending_update = null;
            CancelInvoke("run_pending");
        }
    }

    protected override Sprite empty_sprite()
    {
        // Don't display anything when the armour
        // slot is unoccupied, so that we can see
        // the equipment background illustration.
        return null;
    }
}