using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> The in-game (rather than ui), networked component of an inventory slot. </summary>
public class inventory_slot_networked : networked
{
    public item item => Resources.Load<item>("items/" + net_item.value);
    public string item_name => net_item.value;
    public int count => net_count.value;
    public int index { get => net_index.value; set => net_index.value = value; }

    networked_variables.net_string net_item;
    networked_variables.net_int net_count;
    networked_variables.net_int net_index;

    public override void on_init_network_variables()
    {
        net_item = new networked_variables.net_string();
        net_count = new networked_variables.net_int();
        net_index = new networked_variables.net_int();
    }

    public void set_item_count(item item, int count)
    {
        net_item.value = item.name;
        net_count.value = count;
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(inventory_slot_networked), true)]
    new class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
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