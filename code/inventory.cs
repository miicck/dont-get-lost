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

    public override void on_init_network_variables()
    {
        // Create the UI
        contents = ui_prefab.inst();
        contents.transform.SetParent(FindObjectOfType<Canvas>().transform);
        contents.transform.position = new Vector3(Screen.width / 2, Screen.height / 2, 0);
        ui_open = false; // Starts closed

        net_inventory = networked_variables.net_inventory.new_linked_to(contents);
    }

    public bool ui_open
    {
        get => contents.gameObject.activeInHierarchy;
        set => contents.gameObject.SetActive(value);
    }
}