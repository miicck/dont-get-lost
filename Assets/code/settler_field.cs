using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_field : settler_interactable, INonBlueprintable, INonEquipable
{
    public item_output output;
    public string field_spot_prefab;
    public int size = 2;
    public float spacing = 1f;

    float time_spent_farming;

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

    //##############//
    // INTERACTABLE //
    //##############//

    public override void on_assign(settler s)
    {
        // Reset stuff
        time_spent_farming = 0f;
    }

    public override void on_interact(settler s)
    {
        // Record the amount of time spent farming
        time_spent_farming += Time.deltaTime;
    }

    public override bool is_complete(settler s)
    {
        // It takes 1 second to tend the field
        return time_spent_farming > 1f;
    }

    public override void on_unassign(settler s)
    {
        // Only finish task on authority client
        if (!s.has_authority) return;

        // When completed, tend/harvest the field
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
                        spot.harvest();
                }
            }
    }

    public override string task_info()
    {
        return "Tending to " + GetComponentInParent<item>().display_name;
    }
}