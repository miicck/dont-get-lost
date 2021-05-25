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
        // This might end up being too expensive, in which case we're gonna need to
        // wait for physics updates before testing settler_path_link(s)
        Physics.SyncTransforms();

        var b = collider.bounds;
        b.size += Vector3.one * EDGE_THICKNESS * 2f;

        return b;
    }

    public bool overlaps(settler_path_section other)
    {
        var b1 = linkable_region();
        var b2 = other.linkable_region();

        // Ensure the two bounds are close enough to each other
        // (this avoids annoying diagonal-type links, which don't
        //  make for good paths)
        if ((b1.center - b2.center).magnitude > max_distance)
            return false;

        // First check if the bounds don't overlap
        if (!b1.Intersects(b2)) return false;

        // Then test if a boxcast contains the other section
        Vector3 test_start = transform.position + collider.center +
                             collider.size.y * collider.transform.up;

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

    private void OnDrawGizmosSelected()
    {
        if (collider == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
    }
}
