using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_product : product
{
    public string item;
    public int count;

    public override string crafting_string()
    {
        if (count > 1) return item + " x " + count;
        else return item;
    }

    public override void on_craft(inventory_section to)
    {
        to.add(item, count);
    }

    public override Sprite sprite()
    {
        return Resources.Load<item>("items/" + item).sprite;
    }
}
