using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_field : settler_interactable, INonBlueprintable, INonEquipable
{
    public item_output output;
    public string field_spot_prefab;
    public int size = 2;
    public float spacing = 1f;

    // Get the spots indexed by their coordinates
    settler_field_spot[,] spots
    {
        get
        {
            settler_field_spot[,] ret = new settler_field_spot[size, size];
            foreach (var s in GetComponentsInChildren<settler_field_spot>())
            {
                var c = get_coords(s);
                ret[c[0], c[1]] = s;
            }

            return ret;
        }
    }

    protected override void Start()
    {
        if (output == null)
            throw new System.Exception("Field output not set!");
        base.Start();
    }

    public override bool interact(settler s, float time_elapsed)
    {
        // It takes 1 second to tend the field
        if (time_elapsed < 1f)
            return false;

        var spots = this.spots;
        var locations = this.locations();

        for (int x = 0; x < size; ++x)
            for (int z = 0; z < size; ++z)
            {
                var spot = spots[x, z];
                if (spot == null)
                {
                    // Create a networked spot here
                    client.create(locations[x, z], field_spot_prefab, 
                        rotation: transform.rotation, parent: GetComponent<networked>());
                }
                else
                {
                    // Farm the spot here
                    spot.tend();

                    if (spot.grown)
                    {
                        spot.harvest();
                        foreach (var p in spot.products)
                            p.create_in_node(output);
                    }
                }
            }

        return true;
    }

    int[] get_coords(settler_field_spot spot)
    {
        int[] ret = new int[2];

        float min_dis = Mathf.Infinity;
        var locs = locations();

        for (int x = 0; x < size; ++x)
            for (int z = 0; z < size; ++z)
            {
                float dis = (locs[x, z] - spot.transform.position).sqrMagnitude;
                if (dis < min_dis)
                {
                    min_dis = dis;
                    ret[0] = x;
                    ret[1] = z;
                }
            }

        return ret;
    }

    Vector3[,] locations()
    {
        Vector3[,] ret = new Vector3[size, size];
        for (int x = 0; x < size; ++x)
            for (int z = 0; z < size; ++z)
                ret[x, z] = transform.TransformPoint(
                    (x - size / 2f + 0.5f) * spacing, 0,
                    (z - size / 2f + 0.5f) * spacing
                );

        return ret;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        foreach (var v in locations())
            Gizmos.DrawLine(v, v + Vector3.up / 2f);
    }
}