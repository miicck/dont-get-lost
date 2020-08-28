using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class chest : fixture_with_inventory, ILeftPlayerMenu
{
    protected override string inventory_prefab()
    {
        return "inventories/chest";
    }

    item_link_point input;

    private void Start()
    {
        input = GetComponentInChildren<item_link_point>();

        if (input == null)
            throw new System.Exception("Chest has no item input!");

        if (input.type != item_link_point.TYPE.INPUT)
            throw new System.Exception("Chest input link is of the wrong type!");
    }

    private void Update()
    {
        // Transfer input into chest inventory
        if (input.item == null) return;
        if (has_authority) inventory.add(input.item, 1);
        input.delete_item();
    }

    //#################//
    // ILeftPlayerMenu //
    //#################//

    public RectTransform left_menu_transform() { return inventory?.ui; }
    public void on_left_menu_open() { }
    public void on_left_menu_close() { }
    public inventory editable_inventory() { return inventory; }
}
