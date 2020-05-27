using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class recipe : MonoBehaviour
{
    public product product;
    public List<ingredient> ingredients;

    public crafting_entry get_entry()
    {
        var ce = crafting_entry.create();
        ce.image.sprite = product.sprite();
        ce.text.text = "";
        foreach (var i in ingredients)
            ce.text.text += i.str() + " + ";
        ce.text.text = ce.text.text.Substring(0, ce.text.text.Length - 2);

        ce.text.text += "> " + product.crafting_string();
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
        product.on_craft(to);
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
    public abstract void on_craft(inventory_section to);
    public abstract Sprite sprite();
}