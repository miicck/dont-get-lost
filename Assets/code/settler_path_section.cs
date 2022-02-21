using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_path_section : town_path_link
{
#   if UNITY_EDITOR
    new public BoxCollider collider;
#   else
    public BoxCollider collider;
#   endif
    public float max_distance = Mathf.Infinity;

    const float EDGE_THICKNESS = 0.05f;

    public override Bounds linkable_region()
    {
        var b = overlap_bounds();

        if (b.size.y < CLEARANCE_HEIGHT)
        {
            // Increase height to include clearance height
            float delta_height = CLEARANCE_HEIGHT - b.size.y;
            b.size += Vector3.up * delta_height;
            b.center += Vector3.up * delta_height / 2f;
        }

        return b;
    }

    /// <summary> This is the bounding box for this path section, as 
    /// far as the overlap test is concerned. </summary>
    Bounds overlap_bounds()
    {
        // This might end up being too expensive, in which case we're gonna need to
        // wait for physics updates before testing settler_path_link(s)
        Physics.SyncTransforms();

        var b = collider.bounds;
        b.size += Vector3.one * EDGE_THICKNESS * 2f;

        return b;
    }

    public bool overlaps(settler_path_section other)
    {
        var b1 = overlap_bounds();
        var b2 = other.overlap_bounds();

        // Ensure the two bounds are close enough to each other
        // (this avoids annoying diagonal-type links, which don't
        //  make for good paths)
        if ((b1.center - b2.center).magnitude > Mathf.Max(max_distance, other.max_distance))
            return false;

        // First check if the bounds don't overlap
        if (!b1.Intersects(b2)) return false;

        // Then test if a boxcast contains the other section
        Vector3 test_start = b1.center + collider.size.y * collider.transform.up;

        var hits = Physics.BoxCastAll(
            test_start, // Centre
            collider.size / 2f + Vector3.one * EDGE_THICKNESS, // Half extents
            -collider.transform.up, // Direction
            collider.transform.rotation, // Orientation
            collider.size.y // Max distance
        );

        foreach (var h in hits)
            if (other.collider == h.collider)
                return true;

        return false;
    }

    public float distance_to(Vector3 p)
    {
        return (collider.ClosestPoint(p) - p).magnitude;
    }

    protected override bool ignore_blocking_hit(RaycastHit h)
    {
        // Don't ignore any blocking hits
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (collider == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
    }
}
