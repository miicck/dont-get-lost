using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> The in-game (rather than ui), networked component of an inventory slot. </summary>
public class inventory_slot_networked : networked
{
    // Public getters
    public inventory inventory => GetComponentInParent<inventory>();
    public item item => Resources.Load<item>("items/" + item_name);
    public string item_name => contents.first;
    public int count => contents.second;
    public int index => net_index.value;

    // The actual contents of this slot are networked
    networked_variables.net_int net_index; // The inventory slot coordinate
    networked_variables.simple_networked_pair<string, int> contents; // The actual contents of the slot

    public override void on_init_network_variables()
    {
        net_index = new networked_variables.net_int(default_value: -1);

        contents = new networked_variables.simple_networked_pair<string, int>(
            new networked_variables.net_string(),
            new networked_variables.net_int());

        net_index.on_change_old_new = (old_value, new_value) =>
        {
            // Index value is not allowed to change (there is a 1:1 correspondance
            // between indexed inventory slots and their networked version). However,
            // if the old value is < 0, then this is the first time we're assigning
            // this correspondance, which is obviously allowed and triggers an
            // inventory.on_slot_change.
            if (old_value >= 0 && (old_value != new_value))
                throw new System.Exception("Tried to overwrite slot index!");

            // Only trigger an on_slot_change when the index is first 
            // assigned and my contents are not empty; this happens
            // if the contents are deserialized before the index 
            // (all other changes are handled by contents.on_change, which
            //  requires a valid index to already be set).
            if (old_value <= 0 && (item != null || count != 0))
                inventory.on_slot_change(index, item, count);
        };

        contents.on_change = () =>
        {
            if (net_index.value < 0) return; // Wait for correctly indexed slot
            inventory.on_slot_change(index, item, count);
        };
    }

    public override void on_forget(bool deleted)
    {
        base.on_forget(deleted);

        // We've been deleted => this is now an empty slot
        if (deleted) inventory.on_slot_change(index, null, 0);
    }

    public void set_item_count_index(item item, int count, int index)
    {
        net_index.value = index;
        contents.set(item == null ? "" : item.name, count);
    }

    public void switch_with(inventory_slot_networked other)
    {
        var tmp_item = item_name;
        var tmp_count = count;
        contents.set(other.item_name, other.count);
        other.contents.set(tmp_item, tmp_count);
    }

    public bool add(item item, int count)
    {
        if (item.name == item_name)
        {
            contents.second += count;
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
            if (count >= this.count)
            {
                // Remove all of the items in this slot
                // (by deleting the slot)
                int removed = this.count;
                contents.second = 0;
                delete();
                return removed;
            }
            else
            {
                // Remove count of the items from this slot
                contents.second -= count;
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
    new class editor : networked.editor
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