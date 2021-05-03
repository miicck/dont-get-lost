using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_field : walk_to_settler_interactable, INonBlueprintable, INonEquipable
{
    public item_output output;
    public string field_spot_prefab;
    public int x_size = 2;
    public int z_size = 2;
    public float spacing = 1f;
    public float spot_tend_prob = 0.33f;
    public float base_tend_time = 4f;
    public Vector3 offset = Vector3.zero;

    // Get the spots indexed by their coordinates
    settler_field_spot[,] spots
    {
        get
        {
            settler_field_spot[,] ret = new settler_field_spot[x_size, z_size];
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

        for (int x = 0; x < x_size; ++x)
            for (int z = 0; z < z_size; ++z)
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
        Vector3[,] ret = new Vector3[x_size, z_size];
        for (int x = 0; x < x_size; ++x)
            for (int z = 0; z < z_size; ++z)
                ret[x, z] = transform.TransformPoint(
                    offset.x + (x - x_size / 2f + 0.5f) * spacing,
                    offset.y,
                    offset.z + (z - z_size / 2f + 0.5f) * spacing
                );

        return ret;
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        Gizmos.color = Color.green;
        foreach (var v in locations())
            Gizmos.DrawLine(v, v + Vector3.up / 2f);
    }

    //##############//
    // INTERACTABLE //
    //##############//

    float work_done;
    settler_animations.simple_work work_anim;

    protected override void on_arrive(settler s)
    {
        // Reset stuff
        work_done = 0f;
        work_anim = new settler_animations.simple_work(s,
            period: 1f / total_proficiency_multiplier(s));
    }

    protected override STAGE_RESULT on_interact_arrived(settler s, int stage)
    {
        // Play the work animation
        work_anim.play();

        // Only grow the field on the authority client
        if (!s.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        // Record the amount of time spent farming
        work_done += Time.deltaTime * total_proficiency_multiplier(s);
        if (work_done < base_tend_time) return STAGE_RESULT.STAGE_UNDERWAY;

        // We've spent enough time harvesting, grow/harvest crops
        var spots = this.spots;
        var locations = this.locations();

        for (int x = 0; x < x_size; ++x)
            for (int z = 0; z < z_size; ++z)
            {
                // Only tend to a particular spot with the given spot_tend_prob
                if (Random.Range(0, 1f) > spot_tend_prob)
                    continue;

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

        return STAGE_RESULT.TASK_COMPLETE;
    }

    public override string task_summary()
    {
        return "Tending to " + GetComponentInParent<item>().display_name;
    }
}