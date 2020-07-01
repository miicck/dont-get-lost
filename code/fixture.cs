using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A fixture is a piece of equipment that forms part of a 
/// settlement. Fixtures are associated with a particular bed, forming
/// a house. </summary>
public class fixture : building_material, IInspectable
{
    public Transform settler_stands_here;
    public bed bed { get => GetComponentInParent<bed>(); }

    new public string inspect_info()
    {
        string info = display_name + "\n";
        if (bed == null) info += "Fixture has no associated bed.";
        else info += "Fixture is associated to " + bed.display_name + ".";
        return info;
    }

    new public Sprite main_sprite()
    {
        return sprite;
    }

    new public Sprite secondary_sprite()
    {
        return bed?.sprite;
    }

    protected override networked parent_on_placement()
    {
        // Parent to the nearest bed, if one exists
        return bed.closest_bed(transform.position);
    }
}

public abstract class fixture_with_inventory : fixture
{
    public inventory inventory { get; private set; }

    protected abstract string inventory_prefab();
    protected virtual void on_set_inventory() { }

    public override void on_first_register()
    {
        base.on_first_register();
        client.create(transform.position, inventory_prefab(), this);
    }

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);
        if (child is inventory)
        {
            inventory = (inventory)child;
            on_set_inventory();
        }
    }
}