using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class inventory : MonoBehaviour
{
    inventory_slot[] slots { get { return GetComponentsInChildren<inventory_slot>(); } }

    // Consolodate the inventory slots of this inventory
    public void sort()
    {
        Dictionary<item, int> contents = new Dictionary<item, int>();
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
}
