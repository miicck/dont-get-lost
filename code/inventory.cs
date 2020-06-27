using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class inventory : networked
{
    public RectTransform ui_prefab;

    inventory_slot[] slots;

    public inventory_slot_networked nth_slot(int n)
    {
        foreach (var isn in GetComponentsInChildren<inventory_slot_networked>())
            if (isn.index == n)
                return isn;
        return null;
    }

    public RectTransform ui
    {
        get
        {
            if (_ui == null)
            {
                // Create the ui element
                _ui = ui_prefab.inst();
                _ui.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
                _ui.anchoredPosition = Vector2.zero;

                // Setup the ui slots to link to this inventory
                slots = _ui.GetComponentsInChildren<inventory_slot>();
                for (int i = 0; i < slots.Length; ++i)
                {
                    var isb = slots[i].button.gameObject.AddComponent<inventory_slot_button>();
                    isb.index = i;
                    isb.inventory = this;
                    slots[i].update(null, 0, this); // Initalize ui to empty
                }

                // UI starts closed, is opened using the "open" set method
                _ui.gameObject.SetActive(false);
            }

            return _ui;
        }
    }
    RectTransform _ui;

    /// <summary> Is the ui element currently active? </summary>
    public bool open
    {
        get => ui.gameObject.activeInHierarchy;
        set => ui.gameObject.SetActive(value);
    }

    /// <summary> Forward a click to the appropriate network slot. </summary>
    public void click_slot(int slot_index, bool right_click)
    {
        // Ensure the ui exists
        if (ui == null)
            throw new System.Exception("UI should create itself!");

        var mi = FindObjectOfType<mouse_item>();

        foreach (var isn in GetComponentsInChildren<inventory_slot_networked>())
            if (isn.index == slot_index)
            {
                if (mi != null)
                {
                    // Add the mouse item to the slot
                    if (isn.add(mi.item, mi.count)) mi.count = 0;
                    on_slot_change(slot_index, isn.item, isn.count);
                    return;
                }
                else
                {
                    // Pickup the mouse item from the slot
                    isn.pickup(right_click);
                    return;
                }
            }

        // If we've got here => the slot is not yet networked => it's empty
        if (mi != null && slots[slot_index].accepts(mi.item))
        {
            // We're putting the item(s) in this slot
            // Create a networked slot with the corresponding info
            var isn = (inventory_slot_networked)client.create(
                transform.position, "misc/networked_inventory_slot", this);
            isn.set_item_count_index(mi.item, mi.count, slot_index);
            on_slot_change(slot_index, mi.item, mi.count);
            mi.count = 0;
        }
    }

    List<inventory_slot_networked> children_added = new List<inventory_slot_networked>();
    List<inventory_slot_networked> children_removed = new List<inventory_slot_networked>();

    private void Update()
    {
        // Ensure the ui exists
        if (ui == null)
            throw new System.Exception("UI should create itself!");

        foreach (var slot in children_added)
            on_slot_change(slot.index, slot.item, slot.count);
        children_added.Clear();

        foreach (var slot in children_removed)
            on_slot_change(slot.index, null, 0);
        children_removed.Clear();
    }

    public override void on_delete_networked_child(networked child)
    {
        base.on_delete_networked_child(child);
        children_removed.Add((inventory_slot_networked)child);
    }

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);
        children_added.Add((inventory_slot_networked)child);
    }

    public bool add(string item, int count)
    {
        var to_add = Resources.Load<item>("items/" + item);
        if (to_add == null) throw new System.Exception("Could not find the item " + item);
        return add(to_add, count);
    }

    public bool add(item item, int count)
    {
        // Ensure the ui exists
        if (ui == null)
            throw new System.Exception("UI should create itself!");

        // Attempt to add the item to existing networked slots
        var networked_slots = new HashSet<int>();
        foreach (var isn in GetComponentsInChildren<inventory_slot_networked>())
        {
            if (isn.add(item, count))
                return true;
            networked_slots.Add(isn.index);
        }

        // Find an empty slot to add the item to
        for (int i = 0; i < slots.Length; ++i)
        {
            if (networked_slots.Contains(i)) continue; // This slot is taken
            if (slots[i].accepts(item))
            {
                // Create a networked slot with the corresponding info
                var isn = (inventory_slot_networked)client.create(
                    transform.position, "misc/networked_inventory_slot", this);
                isn.set_item_count_index(item, count, i);
                return true;
            }
        }

        return false;
    }

    public void remove(string item, int count)
    {
        var to_remove = Resources.Load<item>("items/" + item);
        if (to_remove == null) throw new System.Exception("Could not find the item " + item);
        remove(to_remove, count);
    }

    public void remove(item item, int count)
    {
        if (item == null || count == 0)
            return;

        // Run over the occupied (networked) slots, and remove count items
        foreach (var isn in GetComponentsInChildren<inventory_slot_networked>())
        {
            count -= isn.remove(item, count);
            if (count <= 0) break;
        }

        if (count != 0)
            Debug.LogWarning("Items not removed properly!");
    }

    public bool contains(string item, int count = 1)
    {
        var to_test = Resources.Load<item>("items/" + item);
        if (to_test == null) throw new System.Exception("Could not find the item " + item);
        return contains(to_test, count);
    }

    public bool contains(item item, int count = 1)
    {
        // Count the amount of item we have in occupied 
        // (networked) slots and see if that is >= count
        int found = 0;
        foreach (var isn in GetComponentsInChildren<inventory_slot_networked>())
            if (isn.item_name == item.name)
            {
                found += isn.count;
                if (found >= count)
                    return true;
            }

        return false;
    }

    public Dictionary<item, int> contents()
    {
        Dictionary<item, int> ret = new Dictionary<item, int>();
        foreach (var isn in GetComponentsInChildren<inventory_slot_networked>())
        {
            if (!ret.ContainsKey(isn.item)) ret[isn.item] = isn.count;
            else ret[isn.item] += isn.count;
        }
        return ret;
    }

    public delegate void on_change_func();
    List<on_change_func> listeners = new List<on_change_func>();
    public void add_on_change_listener(on_change_func f) { listeners.Add(f); }

    public void on_slot_change(int slot_index, item item, int count)
    {
        slots[slot_index].update(item, count, this);
        foreach (var f in listeners) f();
    }
}