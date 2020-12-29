using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mineshaft : settler_interactable_options
{
    item_output output => GetComponentInChildren<item_output>();

    //##############################//
    // settler_interactable_options //
    //##############################//

    public override string left_menu_display_name() { return "Mineshaft"; }

    protected override int options_count => minable_items.Count;

    protected override option get_option(int i)
    {
        var mi = minable_items[i];
        return new option()
        {
            text = mi.display_name,
            sprite = mi.sprite
        };
    }

    //######################//
    // settler_interactable //
    //######################//

    float time_mining;

    public override void on_assign(settler s)
    {
        time_mining = 0;
    }

    public override void on_interact(settler s)
    {
        if (time_mining + Time.deltaTime >= 1f && time_mining < 1f)
        {
            // This is the tick that will take us past mining
            // time, create the item
            var itm = minable_items[selected_option];
            var op = output;
            op.add_item(item.create(itm.name, op.transform.position, 
                op.transform.rotation, logistics_version: true));
        }

        time_mining += Time.deltaTime;
    }

    public override bool is_complete(settler s)
    {
        return time_mining > 1f;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static List<item> minable_items
    {
        get
        {
            if (_minable_items == null)
            {
                _minable_items = new List<item>();
                foreach (var oim in Resources.LoadAll<obtainable_in_mineshaft>("items/"))
                    _minable_items.Add(oim.GetComponent<item>());
            }
            return _minable_items;
        }
    }
    static List<item> _minable_items;
}