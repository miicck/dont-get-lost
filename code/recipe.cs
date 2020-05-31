using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class recipe : MonoBehaviour
{
    public List<product> products;
    public List<ingredient> ingredients;

    public crafting_entry get_entry()
    {
        var ce = crafting_entry.create();
        ce.image.sprite = products[0].sprite();
        ce.text.text = "";

        foreach (var i in ingredients)
            ce.text.text += i.str() + " + ";
        ce.text.text = ce.text.text.Substring(0, ce.text.text.Length - 2);

        ce.text.text += "> ";

        foreach (var p in products)
            ce.text.text += p.crafting_string() + " + ";
        ce.text.text = ce.text.text.Substring(0, ce.text.text.Length - 2);

        return ce;
    }

    public bool can_craft(inventory_section i)
    {
        foreach (var ing in ingredients)
            if (!ing.in_inventory(i))
                return false;
        return true;
    }

    public void craft(inventory_section from, inventory_section to)
    {
        if (!can_craft(from)) return;
        foreach (var ing in ingredients)
            ing.on_craft(from);
        foreach (var p in products)
            p.create_in_inventory(to);
    }
}

public abstract class ingredient : MonoBehaviour
{
    public abstract string str();
    public abstract bool in_inventory(inventory_section i);
    public abstract void on_craft(inventory_section i);
}

public abstract class product : MonoBehaviour
{
    public abstract string crafting_string();
    public abstract string product_name();
    public abstract string product_name_plural();
    public abstract void create_in_inventory(inventory_section inv);
    public abstract Sprite sprite();
}