using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class inventory : networked
{
    public RectTransform ui_prefab;

    /// <summary> The ui element that contains the inventory slots. </summary>
    public RectTransform ui
    {
        get
        {
            if (_ui == null)
            {
                // Create the ui element and link it's slots to this
                _ui = ui_prefab.inst();
                _ui.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
                _ui.anchoredPosition = Vector2.zero;
                load();

                // Ui starts closed
                open = false;
            }
            return _ui;
        }
    }
    RectTransform _ui;

    void load()
    {
        for (int i = 0; i < slots.Length; ++i)
        {
            var s = slots[i];
            s.index = i;
            s.inventory = this;
            s.update_ui(s.item, s.count);
        }

        // Sync the ui with the networked values
        foreach (var nw in GetComponentsInChildren<inventory_slot_networked>())
        {
            var s = slots[nw.index];
            s.networked = nw;
            s.set_item_count(nw.item, nw.count);
            s.update_ui(nw.item, nw.count);
        }
    }

    public bool open
    {
        get => ui.gameObject.activeInHierarchy;
        set
        {
            load();
            ui.gameObject.SetActive(value);
        }
    }

    public inventory_slot nth_slot(int n)
    {
        if (slots.Length <= n || n < 0) return null;
        return slots[n];
    }

    protected inventory_slot[] slots
    {
        get
        {
            if (_slots == null)
                _slots = ui.GetComponentsInChildren<inventory_slot>();
            return _slots;
        }
    }
    inventory_slot[] _slots;

    public bool add(string item, int count)
    {
        // Load the item that we're adding
        var i = Resources.Load<item>("items/" + item);
        if (i == null) return false;

        // Popup message when adding stuff to local player inventory
        if (this == player.current?.inventory)
            popup_message.create("+ " + count + " " + (count > 1 ? i.plural : i.display_name));

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
    public bool contains(item item, int count = 1)
    {
        return contains(item.name, count);
    }

    public bool contains(string item, int count = 1)
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