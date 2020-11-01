using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class workbench : building_with_inventory, ILeftPlayerMenu
{
    protected override string inventory_prefab() { return "inventories/workbench"; }
    crafting_input crafting;

    //#################//
    // ILeftPlayerMenu //
    //#################//

    public RectTransform left_menu_transform()
    {
        if (inventory == null)
            return null;

        if (crafting == null)
        {
            crafting = inventory.ui.GetComponentInChildren<crafting_input>();
            inventory.ui.GetComponentInChildren<UnityEngine.UI.Text>().text = display_name.capitalize();
            crafting.recipes_folder = "recipes/workbenches/" + name;
            crafting.load_recipies();
            crafting.craft_from = inventory;
            crafting.craft_to = player.current.inventory;
        }

        return inventory.ui;
    }

    public void on_left_menu_open() { }
    public void on_left_menu_close() { }
    public inventory editable_inventory() { return inventory; }
    public recipe[] additional_recipes() { return null; }
}
