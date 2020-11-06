using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class portal : building_material, ILeftPlayerMenu
{
    public Transform teleport_location;

    //#################//
    // ILeftPlayerMenu //
    //#################//

    RectTransform menu;

    public string left_menu_display_name() { return display_name; }

    public RectTransform left_menu_transform()
    {
        if (menu == null)
        {
            // Create the menu
            menu = Resources.Load<RectTransform>("ui/portal").inst();
            menu.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
        }
        return menu;
    }

    public void on_left_menu_open()
    {
        var content = menu.gameObject.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;

        // Destroy the old buttons
        foreach (Transform c in content)
        {
            var b = c.GetComponent<UnityEngine.UI.Button>();
            if (b != null) Destroy(b.gameObject);
        }

        // Load the new buttons
        FindObjectOfType<teleport_manager>().create_buttons(content);
    }

    public void on_left_menu_close() { }
    public inventory editable_inventory() { return null; }
    public recipe[] additional_recipes() { return null; }

    //#################//
    // Unity callbacks //
    //#################//

    private void Start()
    {      
        //InvokeRepeating("create_pulse", 0.2f, 0.2f);
        //InvokeRepeating("spawn_attacker", 1f, 1f);
    }

    public override void on_first_create()
    {
        base.on_first_create();
        FindObjectOfType<teleport_manager>().register_portal(this);
    }

    public string attempt_rename(string new_name)
    {
        return FindObjectOfType<teleport_manager>().attempt_rename_portal(this, new_name);
    }

    public string teleport_name()
    {
        return FindObjectOfType<teleport_manager>().get_portal_name(this);
    }

    public override void on_forget(bool deleted)
    {
        base.on_forget(deleted);
        if (deleted && has_authority)
            FindObjectOfType<teleport_manager>().unregister_portal(this);
    }
}