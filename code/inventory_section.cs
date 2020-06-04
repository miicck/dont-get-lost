using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A UI inventory. Allows manipulation of child inventory slots. </summary>
public class inventory_section : MonoBehaviour
{
    public inventory_slot[] slots
    { get => GetComponentsInChildren<inventory_slot>(true); }

    /// <summary> The point at which sub-menus attach on the left. </summary>
    public RectTransform left_expansion_point;

    /// <summary> Set to true if this inventory is the main player inventory. </summary>
    public bool is_player_inventory;

    /// <summary> Add the given number of a particular item to the inventory. </summary>
    public bool add(string item, int count)
    {
        if (count == 0)
            return true;

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

        slot_found.set_item_count(item, slot_found.count + count);

        if (is_player_inventory)
        {
            item itm = Resources.Load<item>("items/" + item);
            string msg = "+ " + count + " " + (count > 1 ? itm.plural : itm.display_name);
            msg += " (" + contents()[item] + ")";
            popup_message.create(msg);
        }

        return true;
    }

    /// <summary> Remove the given number of a particular item from the inventory. </summary>
    public void remove(string item, int count)
    {
        if (count == 0)
            return;

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

    /// <summary> Set the number of a particular item in this inventory. </summary>
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

    /// <summary> Consolodate the slots of this inventory. </summary>
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

    public void transfer_all_to(inventory_section other)
    {
        foreach (var s in slots)
            if (other.add(s.item, s.count))
                s.clear();
    }

    /// <summary> The contents of this inventory, keyed by item name. </summary>
    public SortedDictionary<string, int> contents()
    {
        var ret = new SortedDictionary<string, int>();
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
        // Call implementation specific listeners
        foreach (var f in on_change_listeners) f();
    }

    public delegate void on_change_func();
    List<on_change_func> on_change_listeners = new List<on_change_func>();

    public void add_on_change_listener(on_change_func f)
    {
        on_change_listeners.Add(f);
    }
}