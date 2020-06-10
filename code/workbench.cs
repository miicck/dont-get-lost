using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class workbench : MonoBehaviour, ILeftPlayerMenu
{
    public string load_recipies_from;

    //#################//
    // ILeftPlayerMenu //
    //#################//

    public RectTransform left_menu_transform()
    {
        if (craft_menu == null)
        {
            craft_menu = Resources.Load<RectTransform>("ui/workbench").inst();
            craft_menu.GetComponentInChildren<UnityEngine.UI.Text>().text = GetComponent<item>().display_name.capitalize();
            craft_menu.GetComponentInChildren<crafting_input>().load_recipies(load_recipies_from);
        }

        return craft_menu;
    }
    RectTransform craft_menu;

    public void on_left_menu_open() { }
    public void on_left_menu_close() { }
}
