using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class chest : building_with_inventory, ILeftPlayerMenu
{
    protected override string inventory_prefab()
    {
        return "inventories/chest";
    }

    item_input input;

    private void Start()
    {
        input = GetComponentInChildren<item_input>();
        if (input == null) throw new System.Exception("Chest has no item input!");
    }

    private void Update()
    {
        // Transfer input into chest inventory
        var next_item = input.release_next_item();
        if (next_item == null) return;
        if (has_authority) inventory?.add(next_item, 1);
        Destroy(next_item.gameObject);
    }

    //#################//
    // ILeftPlayerMenu //
    //#################//

    public string left_menu_display_name() { return display_name; }
    public RectTransform left_menu_transform() { return inventory?.ui; }
    public void on_left_menu_open() { }
    public void on_left_menu_close() { }
    public inventory editable_inventory() { return inventory; }
    public recipe[] additional_recipes() { return null; }
}