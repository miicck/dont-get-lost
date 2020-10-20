using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_field : settler_interactable, INonBlueprintable, INonEquipable
{
    public item_output output;
    settler_field_spot[] spots;

    protected override void Start()
    {
        if (output == null)
            throw new System.Exception("Field output not set!");
        spots = GetComponentsInChildren<settler_field_spot>();
        base.Start();
    }

    public override bool interact(settler s)
    {
        var spot = spots[Random.Range(0, spots.Length)];

        spot.tend();

        if (spot.grown)
        {
            spot.harvest();
            foreach (var p in spot.products)
                p.spawn_in_node(output);
        }

        return true;
    }
}
