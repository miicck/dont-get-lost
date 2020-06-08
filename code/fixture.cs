using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A fixture is a piece of equipment that forms part of a 
/// settlement. Fixtures are associated with a particular bed, forming
/// a house. </summary>
public class fixture : building_material, IInspectable
{
    public bed bed { get => GetComponentInParent<bed>(); }

    public string inspect_info()
    {
        if (bed == null) return "Fixture has no associated bed.";
        return "Fixture is associated to " + bed.display_name + ".";
    }

    public Sprite main_sprite()
    {
        return sprite;
    }

    public Sprite secondary_sprite()
    {
        return bed?.sprite;
    }

    protected override networked parent_on_placement()
    {
        // Parent to the nearest bed, if one exists
        return bed.closest_bed(transform.position);
    }
}
