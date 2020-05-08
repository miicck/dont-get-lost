using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> The networked version of an inventory, which exists 
/// in world space, not UI space. </summary>
public class networked_inventory : networked
{
    public networked_variable.net_string_counts item_counts;

    /// <summary> The inventory this corresponds to. </summary>
    public inventory inventory
    {
        get => _inventory;
        set
        {
            if (_inventory != null)
            {
                if (_inventory == value)
                    return;
                throw new System.Exception("Shouldn't overwrite inventory!");
            }
            _inventory = value;
            _inventory.networked_version = this;
        }
    }
    inventory _inventory;

    public override void on_init_network_variables()
    {
        item_counts = new networked_variable.net_string_counts();
        item_counts.on_change = () =>
        {
            // Keep the inventory synced
            foreach (var kv in item_counts)
                inventory.set(kv.Key, kv.Value);
        };
    }
}