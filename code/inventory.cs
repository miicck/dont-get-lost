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

    public bool add(string item, int count)
    {
        bool ret = contents.add(item, count);
        if (player.current != null && transform.IsChildOf(player.current.transform))
        {
            item itm = Resources.Load<item>("items/" + item);
            string msg = "+ " + count + " " + (count > 1 ? itm.plural : itm.name);
            msg += " (" + contents.contents()[item] + ")";
            popup_message.create(msg);
        }
        return ret;
    }

    public void remove(string item, int count) { contents.remove(item, count); }

    /// <summary> The networked collection of items in the inventory. </summary>
    public networked_variables.net_string_counts item_counts;

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
            item_counts.value = contents.contents();
        });

        item_counts = new networked_variables.net_string_counts();
        item_counts.on_change = () =>
        {
            updating_from_network = true;

            foreach (var kv in item_counts.value)
                contents.set(kv.Key, kv.Value);

            updating_from_network = false;
        };
    }

    public bool ui_open
    {
        get => contents.gameObject.activeInHierarchy;
        set => contents.gameObject.SetActive(value);
    }
}