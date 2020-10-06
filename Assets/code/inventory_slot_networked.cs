using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> The in-game (rather than ui), networked component of an inventory slot. </summary>
public class inventory_slot_networked : networked
{
    // Public getters
    public inventory inventory => GetComponentInParent<inventory>();
    public item item => Resources.Load<item>("items/" + net_item.value);
    public string item_name => net_item.value;
    public int count => net_count.value;
    public int index => net_index.value;

    // The actual contents of this slot are networked
    networked_variables.net_string net_item;
    networked_variables.net_int net_count;
    networked_variables.net_int net_index;

    // For error checking
    bool net_index_set = false;

    public override void on_init_network_variables()
    {
        net_index = new networked_variables.net_int(default_value:-1);
        net_item = new networked_variables.net_string();
        net_count = new networked_variables.net_int();

        net_index.on_change = () =>
        {
            if (!net_index_set)
                net_index_set = true;
            else
                throw new System.Exception("Tried to overwrite slot index!");
        };

        net_item.on_change = () => on_change();
        net_count.on_change = () => on_change();
    }

    void on_change()
    {
        if (net_index.value < 0) return;
        inventory.on_slot_change(index, item, count);
    }

    public void set_item_count_index(item item, int count, int index)
    {
        net_index.value = index;
        net_item.value = item.name;
        net_count.value = count;
    }

    public bool add(item item, int count)
    {
        if (item.name == item_name)
        {
            net_count.value += count;
            return true;
        }
        return false;
    }

    /// <summary> Remove at most <paramref name="count"/> of the given
    /// item from the inventory. Returns the amount removed. </summary>
    public int remove(item item, int count)
    {
        if (item.name == item_name)
        {
            if (count >= net_count.value)
            {
                // Remove all of the items in this slot
                // (by deleting the slot)
                int removed = net_count.value;
                net_count.value = 0;
                delete();
                return removed;
            }
            else
            {
                // Remove count of the items from this slot
                net_count.value -= count;
                return count;
            }
        }
        
        // Nothing has been removed
        return 0;
    }

    public void pickup(bool right_click)
    {
        int to_pickup = right_click ? Mathf.Max(count / 2, 1) : count;
        int remaining = count - to_pickup;
        item item_to_pickup = item;
        int index = net_index.value;
        inventory inv = inventory;

        if (remaining == 0) net_item.value = "";
        net_count.value = remaining;

        // Delete this inventory slot + replace it with the
        // updated count.
        delete(() =>
        {
            if (remaining > 0)
            {
                // Create the replacement slot
                var slot = (inventory_slot_networked)client.create(
                    inv.transform.position, "misc/networked_inventory_slot", inv);
                slot.set_item_count_index(item_to_pickup, remaining, index);
            }

            // Update the ui to reflect that the object has been picked up
            mouse_item.create(item_to_pickup, to_pickup, inv);
        });
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(inventory_slot_networked), true)]
    new class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (!Application.isPlaying) return;
            var isn = (inventory_slot_networked)target;
            UnityEditor.EditorGUILayout.TextArea(
                "Item = " + isn.item_name + "\n" +
                "Count = " + isn.count + "\n" +
                "Slot = " + isn.index
            );
        }
    }
#endif
}