using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_product : product
{
    public item item;
    public int count;

    public override string crafting_string()
    {
        if (count > 1) return count + item.plural;
        else return item.display_name();
    }

    public override void on_craft(inventory_section to)
    {
        to.add(item.name, count);
    }

    public override Sprite sprite()
    {
        return item.sprite;
    }
}
