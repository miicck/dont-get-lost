using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class workbench : fixture, ILeftPlayerMenu
{
    public inventory inventory { get; private set; }

    public override void on_first_register()
    {
        base.on_first_register();
        client.create(transform.position, "inventories/workbench", this);
    }

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);
        if (child is inventory)
            inventory = (inventory)child;
    }

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
            crafting.load_recipies("recipes/workbenches/"+name);
            crafting.craft_from = inventory;
            crafting.craft_to = player.current.inventory;
        }

        return inventory.ui;
    }

    public void on_left_menu_open() { }

    public void on_left_menu_close()
    {
        /*
        // Return contents to player inventory
        var inv = craft_menu.GetComponentInChildren<inventory>();
        var contents = inv.contents();
        foreach (var kv in contents)
            if (player.current.inventory.add(kv.Key, kv.Value))
                inv.remove(kv.Key, kv.Value);
                */
    }
}
