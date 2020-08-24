using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item : networked, IInspectable
{
    //###########//
    // VARIABLES //
    //###########//

    public Sprite sprite; // The sprite represeting this item in inventories etc
    public string plural;
    public int value;
    public int fuel_value = 0;
    public float logistics_scale = 1f; // How much to scale the item by when it is in the logistics network
    public Transform carry_pivot { get; private set; } // The point we are carrying this item by in carry mode

    public string display_name
    {
        get => name.Replace('_', ' ');
    }

    public string singular_or_plural(int count)
    {
        if (count == 1) return display_name;
        return plural;
    }

    //############//
    // PLAYER USE //
    //############//

    public struct use_result
    {
        public bool underway;
        public bool allows_look;
        public bool allows_move;
        public bool allows_throw;

        public static use_result complete => new use_result()
        {
            underway = false,
            allows_look = true,
            allows_move = true,
            allows_throw = true
        };

        public static use_result underway_allows_none => new use_result()
        {
            underway = true,
            allows_look = false,
            allows_move = false,
            allows_throw = false
        };

        public static use_result underway_allows_all => new use_result()
        {
            underway = true,
            allows_look = true,
            allows_move = true,
            allows_throw = true
        };

        public static use_result underway_allows_look_only => new use_result()
        {
            underway = true,
            allows_look = true,
            allows_move = false,
            allows_throw = false
        };
    }

    // Use the equipped version of this item
    public virtual use_result on_use_start(player.USE_TYPE use_type) { return use_result.complete; }
    public virtual use_result on_use_continue(player.USE_TYPE use_type) { return use_result.complete; }
    public virtual void on_use_end(player.USE_TYPE use_type) { }
    public virtual bool allow_left_click_held_down() { return false; }
    public virtual bool allow_right_click_held_down() { return false; }

    public virtual Dictionary<string, int> add_to_inventory_on_pickup()
    {
        var ret = new Dictionary<string, int>();
        ret[name] = 1;
        return ret;
    }

    public void pick_up()
    {
        if (!can_pick_up(out string message))
        {
            popup_message.create("Cannot pick up " + display_name + ": " + message);
            return;
        }

        // Delete the object on the network / add it to
        // inventory only if succesfully deleted on the
        // server. This stops two clients from simultaneously
        // deleting an object to duplicate it.
        var to_pickup = add_to_inventory_on_pickup();
        delete(() =>
        {
            foreach (var kv in to_pickup)
                player.current.inventory.add(kv.Key, kv.Value);
        });
    }

    protected virtual bool can_pick_up(out string message)
    {
        message = null;
        return true;
    }

    //############//
    // NETWORKING //
    //############//

    public networked_variables.net_quaternion networked_rotation;

    public override void on_init_network_variables()
    {
        // Create newtorked variables
        networked_rotation = new networked_variables.net_quaternion();
        transform.rotation = Quaternion.identity;
        networked_rotation.on_change = () => transform.rotation = networked_rotation.value;
    }

    public override void on_create()
    {
        // Initialize networked variables
        networked_rotation.value = transform.rotation;
    }

    public override void on_network_update()
    {
        // Keep networked variables up to date
        if (has_authority)
        {
            networked_position = transform.position;
            networked_rotation.value = transform.rotation;
        }
    }

    //##############//
    // IInspectable //
    //##############//

    public virtual string inspect_info() { return display_name; }
    public virtual Sprite main_sprite() { return sprite; }
    public virtual Sprite secondary_sprite() { return null; }

    //################//
    // STATIC METHODS //
    //################//

    /// <summary> Create an item. </summary>
    public static item create(string name,
        Vector3 position, Quaternion rotation,
        bool kinematic = true, bool networked = false,
        networked network_parent = null)
    {
        item item = null;

        if (networked)
        {
            item = (item)client.create(position, "items/" + name,
                rotation: rotation, parent: network_parent);
        }
        else
        {
            item = Resources.Load<item>("items/" + name);
            if (item == null)
                throw new System.Exception("Could not find the item: " + name);
            item = item.inst();
            item.transform.position = position;
            item.transform.rotation = rotation;
            item.transform.SetParent(network_parent == null ? null : network_parent.transform);
        }

        return item;
    }

    public static string item_quantity_info(item item, int quantity)
    {
        if (item == null || quantity == 0)
            return "No item.";

        // Title
        string info = (quantity < 2 ? item.display_name :
            (utils.int_to_comma_string(quantity) + " " + item.plural)) + "\n";

        // Value
        if (quantity > 1)
            info += "  Value : " + (item.value * quantity).qs() + " (" + item.value.qs() + " each)\n";
        else
            info += "  Value : " + item.value.qs() + "\n";

        // Tool type + quality
        if (item is tool)
        {
            var t = (tool)item;
            info += "  Tool type : " + tool.type_to_name(t.type) + "\n";
            info += "  Quality : " + tool.quality_to_name(t.quality) + "\n";
        }

        // Melee weapon info
        if (item is melee_weapon)
        {
            var m = (melee_weapon)item;
            info += "  Melee damage : " + m.damage + "\n";
        }

        // Can this item be built with
        if (item is building_material)
            info += "  Can be used for building\n";

        // Fuel value
        if (item.fuel_value > 0)
        {
            if (quantity > 1)
                info += "  Fuel value : " + (item.fuel_value * quantity).qs() + " (" + item.fuel_value.qs() + " each)\n";
            else
                info += "  Fuel value : " + item.fuel_value.qs() + "\n";
        }

        return utils.allign_colons(info);
    }
}