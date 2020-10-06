using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class sack : networked, ILeftPlayerMenu, IInspectable
{
    inventory inventory;

    public inventory editable_inventory() { return inventory; }
    public RectTransform left_menu_transform() { return inventory.ui; }
    public void on_left_menu_open()
    {
        inventory.ui.GetComponentInChildren<UnityEngine.UI.Text>().text = display_name.value;
    }

    networked_variables.net_string display_name;

    public override void on_init_network_variables()
    {
        display_name = new networked_variables.net_string("sack");
    }

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);

        if (child is inventory)
            inventory = (inventory)child;
    }

    public void on_left_menu_close()
    {
        if (inventory.is_empty())
            delete();
    }

    public string inspect_info()
    {
        return display_name.value;
    }

    public Sprite main_sprite() { return null; }
    public Sprite secondary_sprite() { return null; }

    //##############//
    // STATIC STUFF //
    //##############//

    public static sack create(Vector3 location, 
        IEnumerable<KeyValuePair<item, int>> contents = null, 
        string display_name = "sack")
    {
        var sack = client.create(location, "misc/sack").GetComponent<sack>();
        sack.display_name.value = display_name;

        sack.add_register_listener(() =>
        {
            client.create(location, "inventories/sack", sack);
           
            if (contents != null)
                sack.inventory.add_register_listener(() =>
                {
                    foreach (var kv in contents)
                        sack.inventory.add(kv.Key, kv.Value);
                });
        });
    
        return sack;
    }
}
