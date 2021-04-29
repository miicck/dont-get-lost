using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ladder : MonoBehaviour, INonEquipable, INonBlueprintable
{
    public BoxCollider ladder_collider;
    static ladder_collection ladders = new ladder_collection();
    public static bool in_ladder_volume(Vector3 point) => ladders.has_overlapping(point);

    private void Start()
    {
        // Delay registration until the BoxCollider has had a physics update
        Invoke("register", 0.1f);
    }

    void register()
    {
        ladders.add(this);
    }

    private void OnDestroy()
    {
        ladders.remove(this);
    }

    private void OnDrawGizmos()
    {
        if (ladder_collider == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(ladder_collider.bounds.center, ladder_collider.bounds.size);
    }

    class ladder_collection : spatial_collection<ladder>
    {
        protected override Vector3 get_centre(ladder t) { return t.ladder_collider.bounds.center; }
        protected override float grid_resolution => 2f;

        protected override bool test_intersection(ladder t, Bounds bounds)
        {
            return t.ladder_collider.bounds.Intersects(bounds);
        }

        protected override bool test_intersection(ladder t, Vector3 point)
        {
            return t.ladder_collider.bounds.Contains(point);
        }
    }
}