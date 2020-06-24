using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(networked))]
public class inventory : inventory_section
{
    public RectTransform ui_prefab;

    protected override inventory_slot[] load_slots()
    {
        return ui.GetComponentsInChildren<inventory_slot>();
    }

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
        for(int i=0; i<slots.Length; ++i)
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
}