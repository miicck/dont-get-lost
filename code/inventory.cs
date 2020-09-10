using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IItemCollection
{
    Dictionary<item, int> contents();
    bool add(item i, int count);
    bool remove(item i, int count);
}

// "Of course, that's the business we are in as a common language 
// runtime, but we haven't got around to doing it for MI yet"
public static class item_collection_extensions
{
    public static bool contains(this IItemCollection col, item i, int count = 1)
    {
        int found = 0;
        foreach (var kv in col.contents())
            if (kv.Key.name == i.name)
            {
                found += kv.Value;
                if (found >= count)
                    return true;
            }

        return false;
    }

    public static bool remove(this IItemCollection col, string item_name, int count)
    {
        return col.remove(Resources.Load<item>("items/" + item_name), count);
    }

    public static bool add(this IItemCollection col, string item_name, int count)
    {
        return col.add(Resources.Load<item>("items/" + item_name), count);
    }

    public static int count(this IItemCollection col, item i)
    {
        var cts = col.contents();
        if (cts.TryGetValue(i, out int found))
            return found;
        return 0;
    }

    public static item remove_first(this IItemCollection col)
    {
        foreach (var kv in col.contents())
        {
            if (col.remove(kv.Key, 1))
                return kv.Key;
            return null;
        }
        return null;
    }

    public static void clear(this IItemCollection col)
    {
        var cts = col.contents();
        foreach (var kv in cts)
            col.remove(kv.Key, kv.Value);
    }
}

/// <summary> A simple, dictionary-based
/// implementation of an item collection. </summary>
class simple_item_collection : IItemCollection
{
    Dictionary<string, int> items = new Dictionary<string, int>();

    public Dictionary<item, int> contents()
    {
        Dictionary<item, int> ret = new Dictionary<item, int>();
        foreach (var kv in items)
            ret.Add(Resources.Load<item>("items/" + kv.Key), kv.Value);
        return ret;
    }

    public bool add(item i, int count)
    {
        if (i == null || count == 0) return true;
        if (items.ContainsKey(i.name))
            items[i.name] += count;
        else
            items[i.name] = count;
        return true;
    }

    public bool remove(item i, int count)
    {
        if (items.TryGetValue(i.name, out int found))
        {
            if (found > count)
            {
                items[i.name] -= count;
                return true;
            }
            items.Remove(i.name);
            return found == count;
        }
        else return false;
    }
}

public class inventory : networked, IItemCollection
{
    public RectTransform ui_prefab;

    void generate_ui()
    {
        // Create the ui element
        _ui = ui_prefab.inst();
        _ui.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
        _ui.anchoredPosition = Vector2.zero;

        // Setup the ui slots to link to this inventory
        _slots = _ui.GetComponentsInChildren<inventory_slot>();
        for (int i = 0; i < _slots.Length; ++i)
        {
            var isb = _slots[i].button.gameObject.AddComponent<inventory_slot_button>();
            isb.index = i;
            isb.inventory = this;
            _slots[i].update(null, 0, this); // Initalize ui to empty
        }

        // UI starts closed, is opened using the "open" set method
        _ui.gameObject.SetActive(false);
    }

    /// <summary> The ui element representing this inventory. </summary>
    public RectTransform ui
    {
        get
        {
            if (_ui == null) generate_ui();
            return _ui;
        }
    }
    RectTransform _ui;

    /// <summary> The UI slots representing this inventory. </summary>
    inventory_slot[] slots
    {
        get
        {
            if (_slots == null) generate_ui();
            return _slots;
        }
    }
    inventory_slot[] _slots;

    public inventory_slot_networked nth_slot(int n)
    {
        foreach (var isn in GetComponentsInChildren<inventory_slot_networked>())
            if (isn.index == n)
                return isn;
        return null;
    }

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
                    if (isn.item_name == mi.item.name)
                    {
                        // Add the mouse item to the slot
                        isn.add(mi.item, mi.count);
                        mi.count = 0;
                        on_slot_change(slot_index, isn.item, isn.count);
                    }
                    else
                    {
                        // Switch the mouse item with that in the slot
                        if (slots[slot_index].accepts(mi.item))
                        {
                            item to_pickup = isn.item;
                            int quantity = isn.count;
                            isn.set_item_count_index(mi.item, mi.count, slot_index);
                            mi.count = 0;
                            mouse_item.create(to_pickup, quantity, this);
                            on_slot_change(slot_index, isn.item, isn.count);
                        }
                    }

                    return;
                }
                else
                {
                    // Move the contents of the slot directly into the player inventory
                    // (or from the player inventory to the interacting inventory)
                    if (controls.key_down(controls.BIND.QUICK_ITEM_TRANSFER))
                    {
                        // Copies for lambda function
                        item transfer_item = isn.item;
                        int transfer_count = isn.count;
                        int transfer_slot = slot_index;

                        // Transfer into player inventory, or out of player inventory
                        inventory target = player.current.inventory;
                        if (this == player.current.inventory)
                        {
                            // Transfer out of player inventory, prioritising
                            // left_menu inventory, then the player crafting menu.
                            target = player.current.left_menu?.editable_inventory();
                            if (target == null) target = player.current.crafting_menu;
                        }

                        if (target != null && target.can_add(transfer_item, transfer_count))
                            isn.delete(() =>
                            {
                                // Transfer into target inventory
                                target.add(transfer_item, transfer_count);
                                on_slot_change(transfer_slot, null, 0);
                            });

                        return;
                    }

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

    public override void on_delete_networked_child(networked child)
    {
        base.on_delete_networked_child(child);

        // Networked slot deleted => An inventory slot was cleared
        var isn = (inventory_slot_networked)child;
        on_slot_change(isn.index, isn.item, isn.count);
    }

    public bool can_add(string item, int count)
    {
        var to_add = Resources.Load<item>("items/" + item);
        if (to_add == null) throw new System.Exception("Could not find the item " + item);
        return can_add(to_add, count);
    }

    public bool can_add(item item, int count)
    {
        if (item == null || count == 0)
            return true;

        // Ensure we're adding the prefab version of this item
        item = Resources.Load<item>("items/" + item.name);

        // See if a slot for this item already exists
        var networked_slots = new HashSet<int>();
        foreach (var isn in GetComponentsInChildren<inventory_slot_networked>())
        {
            if (isn.item_name == item.name) return true;
            networked_slots.Add(isn.index);
        }

        // See if there is a free slot to put the item in
        for (int i = 0; i < slots.Length; ++i)
        {
            if (networked_slots.Contains(i)) continue; // This slot is taken
            if (slots[i].accepts(item))
                return true;
        }

        return false;
    }

    public bool add(string item, int count)
    {
        var to_add = Resources.Load<item>("items/" + item);
        if (to_add == null) throw new System.Exception("Could not find the item " + item);
        return add(to_add, count);
    }

    public bool add(item item, int count)
    {
        if (item == null || count == 0)
            return true;

        // Ensure we're adding the prefab version of the item
        item = Resources.Load<item>("items/" + item.name);

        bool added = false;

        // Attempt to add the item to existing networked slots
        var networked_slots = new HashSet<int>();
        foreach (var isn in GetComponentsInChildren<inventory_slot_networked>())
        {
            if (isn.add(item, count))
            {
                added = true;
                break;
            }
            networked_slots.Add(isn.index);
        }

        if (!added)
        {
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
                    added = true;
                    break;
                }
            }
        }

        // If this is the local player inventory, display a message on success
        if (added && GetComponentInParent<player>() == player.current)
        {
            string msg = "+ " + count.qs() + " " + (count > 1 ? item.plural : item.display_name) +
                         " (" + contents()[item] + ")";
            popup_message.create(msg);
        }

        return added;
    }

    public bool remove(string item, int count)
    {
        var to_remove = Resources.Load<item>("items/" + item);
        if (to_remove == null) throw new System.Exception("Could not find the item " + item);
        return remove(to_remove, count);
    }

    public bool remove(item item, int count)
    {
        if (item == null || count == 0)
            return true;

        // Ensure we're removing the prefab version of the item
        item = Resources.Load<item>("items/" + item.name);

        // Run over the occupied (networked) slots, and remove count items
        foreach (var isn in GetComponentsInChildren<inventory_slot_networked>())
        {
            count -= isn.remove(item, count);
            if (count <= 0) break;
        }

        if (count != 0)
            throw new System.Exception("Items not removed properly!");

        return true;
    }

    public bool contains(string item, int count = 1)
    {
        var to_test = Resources.Load<item>("items/" + item);
        if (to_test == null) throw new System.Exception("Could not find the item " + item);
        return item_collection_extensions.contains(this, to_test, count);
    }

    public bool empty
    {
        get
        {
            foreach (var isn in GetComponentsInChildren<inventory_slot_networked>())
                if (isn.item != null && isn.count > 0)
                    return false;
            return true;
        }
    }

    public Dictionary<item, int> contents()
    {
        Dictionary<item, int> ret = new Dictionary<item, int>();
        foreach (var isn in GetComponentsInChildren<inventory_slot_networked>())
        {
            if (isn == null) continue; // Destroyed
            if (isn.item == null) continue; // No item

            // Add the contents to the dictionary
            if (!ret.ContainsKey(isn.item)) ret[isn.item] = isn.count;
            else ret[isn.item] += isn.count;
        }
        return ret;
    }

    public int count(string item)
    {
        return item_collection_extensions.count(this,
            Resources.Load<item>("items/" + item));
    }

    public delegate void on_change_func();
    List<on_change_func> listeners = new List<on_change_func>();
    public void add_on_change_listener(on_change_func f) { listeners.Add(f); }

    public void on_slot_change(int slot_index, item item, int count)
    {
        // Ensure the ui exists
        if (ui == null)
            throw new System.Exception("UI should create itself!");

        slots[slot_index].update(item, count, this);
        foreach (var f in listeners) f();
    }
}