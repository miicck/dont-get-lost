using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class workbench : MonoBehaviour, ILeftPlayerMenu
{
    //#################//
    // ILeftPlayerMenu //
    //#################//

    public RectTransform left_menu_transform()
    {
        if (craft_menu == null)
        {
            craft_menu = Resources.Load<RectTransform>("ui/workbench").inst();
            craft_menu.GetComponentInChildren<UnityEngine.UI.Text>().text = GetComponent<item>().display_name.capitalize();
            var crafting_input = craft_menu.GetComponentInChildren<crafting_input>();
            crafting_input.load_recipies("recipes/workbenches/"+name);
            crafting_input.craft_to = player.current.inventory.contents;
        }

        return craft_menu;
    }
    RectTransform craft_menu;

    public void on_left_menu_open() { }

    public void on_left_menu_close()
    {
        // Return contents to player inventory
        var inv = craft_menu.GetComponentInChildren<inventory_section>();
        var contents = inv.contents();
        foreach (var kv in contents)
            if (player.current.inventory.add(kv.Key, kv.Value))
                inv.remove(kv.Key, kv.Value);
    }
}
