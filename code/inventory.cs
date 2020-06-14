using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An in-game collection of items, linked to the UI element <see cref="contents"/>. </summary>
public class inventory : networked
{
    public inventory_slot[] slots { get => contents.slots; }

    public inventory_section ui_prefab;

    /// <summary> The UI object that has the inventory slots. </summary>
    public inventory_section contents { get; private set; }

    /// <summary> Returns the number of a given item in the inventory. </summary>
    public int count(string item)
    {
        var cts = contents.contents();
        if (!cts.TryGetValue(item, out int ret))
            ret = 0;
        return ret;
    }

    public bool add(string item, int count)
    {
        bool ret = contents.add(item, count);
        return ret;
    }

    public void remove(string item, int count) { contents.remove(item, count); }

    /// <summary> The networked collection of items in the inventory. </summary>
    public networked_variables.net_inventory net_inventory;

    bool updating_from_network = false;
    public override void on_init_network_variables()
    {
        // Create the UI
        contents = ui_prefab.inst();
        contents.transform.SetParent(FindObjectOfType<Canvas>().transform);
        contents.transform.position = new Vector3(Screen.width / 2, Screen.height / 2, 0);
        ui_open = false; // Starts closed

        contents.add_on_change_listener(() =>
        {
            if (updating_from_network) return;

            // Keep the network up up to date with changes
            var new_values = new Dictionary<int, KeyValuePair<string, int>>();
            for (int i = 0; i < slots.Length; ++i)
                if (slots[i].item != null)
                    new_values[i] = new KeyValuePair<string, int>(slots[i].item, slots[i].count);
            net_inventory.value = new_values;
        });

        net_inventory = new networked_variables.net_inventory();
        net_inventory.on_change = () =>
        {
            updating_from_network = true;

            // Update the slot contents from the network
            for (int i = 0; i < slots.Length; ++i)
            {
                if (net_inventory.value.TryGetValue(i, out KeyValuePair<string, int> name_contents))
                    slots[i].set_item_count(name_contents.Key, name_contents.Value);
                else
                    slots[i].clear();
            }

            updating_from_network = false;
        };
    }

    public bool ui_open
    {
        get => contents.gameObject.activeInHierarchy;
        set => contents.gameObject.SetActive(value);
    }
}