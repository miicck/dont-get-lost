using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An inventory is a UI element and is essentially just a collection of 
/// inventory slots. </summary>
public class inventory : MonoBehaviour
{
    /// <summary> The networked version of this inventory (if it exists). </summary>
    public networked_inventory networked_version
    {
        get => _networked_version;
        set
        {
            if (_networked_version != null)
            {
                if (_networked_version == value)
                    return;
                throw new System.Exception("Shouldn't overwrite networked version!");
            }
            _networked_version = value;
            _networked_version.inventory = this;
        }
    }
    networked_inventory _networked_version;

    public inventory_slot[] slots { get { return GetComponentsInChildren<inventory_slot>(); } }

    public bool is_player_inventory = false;
    public RectTransform ui_extend_left_point;

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

        slot_found.set_item_count(item, slot_found.count + count);
        return true;
    }

    public void remove(string item, int count)
    {
        int total_removed = 0;
        foreach (var s in slots)
            if (s.item == item)
            {
                int to_remove = Mathf.Min(count, s.count);
                s.set_item_count(s.item, s.count - to_remove);
                total_removed += to_remove;
                if (total_removed >= count)
                    return;
            }
    }

    public void set(string item, int count)
    {
        int total = 0;
        foreach (var s in slots)
            if (s.item == item)
                total += s.count;

        if (total > count)
            remove(item, total - count);
        else if (total < count)
            add(item, count - total);
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
            s.clear();

        int i = 0;
        foreach (var kv in contents)
        {
            var s = slots[i];
            s.set_item_count(kv.Key, kv.Value);
            ++i;
        }
    }

    /// <summary> The contents of this inventory, keyed by item name. </summary>
    public Dictionary<string, int> contents()
    {
        var ret = new Dictionary<string, int>();
        foreach (var s in slots)
        {
            if (s.item == null || s.count == 0) continue;
            if (!ret.ContainsKey(s.item)) ret[s.item] = s.count;
            else ret[s.item] += s.count;
        }
        return ret;
    }

    /// <summary> Called by slots when their value changes. </summary>
    public void on_change()
    {
        foreach (var f in on_change_listeners) f();

        // Update the networked version of the inventory
        networked_version?.item_counts.set(contents());
    }

    public delegate void on_change_func();
    List<on_change_func> on_change_listeners = new List<on_change_func>();

    public void add_on_change_listener(on_change_func f)
    {
        on_change_listeners.Add(f);
    }
}