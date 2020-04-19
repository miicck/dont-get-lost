using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// An inventory is essentially just a collection of inventory_slots
public class inventory : MonoBehaviour
{
    public inventory_slot[] slots { get { return GetComponentsInChildren<inventory_slot>(); } }

    public bool is_player_inventory = false;

    public bool add(string item, int count)
    {
        inventory_slot slot_found = null;
        foreach (var s in slots)
        {
            if (s.item == null && slot_found == null)
                slot_found = s;

            if (s.item == item)
            {
                slot_found = s;
                break;
            }
        }

        if (slot_found == null)
        {
            popup_message.create("Could not add item " + item + " to inventory " + name + "!");
            return false;
        }

        if (is_player_inventory)
            popup_message.create("+" + count + " " + item);

        slot_found.item = item;
        slot_found.count += count;
        on_change();
        return true;
    }

    public void remove(string item, int count)
    {
        int total_removed = 0;
        foreach (var s in slots)
            if (s.item == item)
            {
                int to_remove = Mathf.Min(count, s.count);
                s.count -= to_remove;
                total_removed += to_remove;
                if (total_removed >= count)
                {
                    on_change();
                    return;
                }
            }

        if (total_removed > 0)
            on_change();
    }

    // Consolodate the inventory slots of this inventory
    public void sort()
    {
        Dictionary<string, int> contents = new Dictionary<string, int>();
        foreach (var s in slots)
        {
            if (s.item == null || s.count == 0) continue;
            if (!contents.ContainsKey(s.item))
                contents[s.item] = 0;
            contents[s.item] += s.count;
        }

        foreach (var s in slots)
            s.count = 0;

        int i = 0;
        foreach (var kv in contents)
        {
            var s = slots[i];
            s.item = kv.Key;
            s.count = kv.Value;
            ++i;
        }
    }

    public void on_change()
    {
        foreach (var f in on_change_listeners) f();
    }

    public delegate void on_change_func();
    List<on_change_func> on_change_listeners = new List<on_change_func>();

    public void add_on_change_listener(on_change_func f)
    {
        on_change_listeners.Add(f);
    }
}
