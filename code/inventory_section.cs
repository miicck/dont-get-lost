using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Allows manipuation of a collection of inventory_slots, as well
/// as seperating an inventory into multiple sections. </summary>
public class inventory_section : MonoBehaviour
{
    protected inventory_slot[] slots
    {
        get
        {
            if (_slots == null)
                _slots = load_slots();
            return _slots;
        }
    }
    inventory_slot[] _slots;

    protected virtual inventory_slot[] load_slots()
    {
        return GetComponentsInChildren<inventory_slot>();
    }

    public bool add(string item, int count)
    {
        // Load the item that we're adding
        var i = Resources.Load<item>("items/" + item);
        if (i == null) return false;

        // First attempt to find a matching slot
        foreach (var s in slots)
            if (s.item?.name == item)
            {
                if (s.accepts(i))
                {
                    s.set_item_count(i, s.count + count);
                    return true;
                }
            }

        // Then settle for any compatible slot
        foreach (var s in slots)
            if (s.accepts(i))
            {
                s.set_item_count(i, s.count + count);
                return true;
            }

        return false;
    }

    public void remove(string item, int count)
    {
        // Remove this many items from the slots
        foreach (var s in slots)
            if (s.item?.name == item)
            {
                int to_remove = Mathf.Min(count, s.count);
                s.set_item_count(s.item, s.count - to_remove);
                count -= to_remove;
                if (count <= 0)
                    break;
            }

        if (count > 0)
            Debug.LogWarning("Did not remove the requested number of items!");
    }

    /// <summary> Check if this section contains the given
    /// item (and at least the given quantity). </summary>
    public bool contains(item item, int count=1)
    {
        return contains(item.name, count);
    }

    public bool contains(string item, int count=1)
    {
        var c = contents();
        if (c.TryGetValue(item, out int found))
            return found >= count;
        return false;
    }

    /// <summary> Return a dictionary containing all of the items
    /// in my slots and their total quantities. </summary>
    public Dictionary<string, int> contents()
    {
        Dictionary<string, int> ret = new Dictionary<string, int>();
        foreach (var s in slots)
        {
            if (s.item == null) continue;
            if (!ret.ContainsKey(s.item.name)) ret[s.item.name] = s.count;
            else ret[s.item.name] += s.count;
        }
        return ret;
    }

    /// <summary> Add a listener to all of my slots. </summary>
    public void add_on_change_listener(inventory_slot.func f)
    {
        foreach (var s in slots)
            s.add_on_change_listener(f);
    }
}