using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class rail : MonoBehaviour
{
    /// <summary> The maximum distance between two snap points on different
    /// rails for them to be considered attached. </summary>
    public const float SNAP_ATTACH_RANGE = 0.1f;

    /// <summary> The end-to-end length of this rail. </summary>
    public float length { get; private set; }

    /// <summary> The other rails that I am attached to. </summary>
    HashSet<rail> attached_to = new HashSet<rail>();

    /// <summary> The building snap points at the ends of this rail. </summary>
    snap_point[] snap_points => GetComponentsInChildren<snap_point>();

    /// <summary> Returns true if this rail can connect 
    /// to the <paramref name="other"/> rail. </summary>
    bool can_attach_to(rail other)
    {
        if (this == null || other == null || other == this) return false;

        foreach (var s in snap_points)
            foreach (var s2 in other.snap_points)
            {
                Vector3 delta = s.transform.position - s2.transform.position;
                if (delta.magnitude < SNAP_ATTACH_RANGE)
                    return true;
            }

        return false;
    }

    /// <summary> Get the position that is <paramref name="progress"/> \in [0, 1] of the way along
    /// this rail in the direction of the given <paramref name="next"/> rail. </summary>
    public Vector3 progress_towards(rail next, float progress)
    {
        Vector3 delta = next.transform.position - transform.position;
        float sign = Mathf.Sign(Vector3.Dot(delta, transform.forward));
        return transform.position + sign * length * transform.forward * (progress - 0.5f);
    }

    /// <summary> Return the next rail I'm connected to closest 
    /// to the given <paramref name="direction"/>. Returns null if
    /// there isn't a rail within 90 degrees of direction from
    /// this rail. </summary>
    public rail next(Vector3 direction)
    {
        return utils.find_to_min(attached_to, (n) =>
        {
            float angle = Vector3.Angle(direction, n.transform.position - transform.position);
            if (angle > 91f) return Mathf.Infinity;
            return angle;
        });
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    bool is_equipped => GetComponentInParent<player>() != null;
    bool is_blueprint => GetComponentInParent<blueprint>() != null;

    private void Start()
    {
        // Don't register equipped/blueprint versions
        if (is_equipped || is_blueprint) return;

        if (snap_points.Length < 2)
            throw new System.Exception("A rail must have at least 2 snap points!");

        length = (snap_points[0].transform.position - snap_points[1].transform.position).magnitude;
        register(this);
    }

    private void OnDestroy()
    {
        // Don't un-register equipped/blueprint versions
        if (is_equipped || is_blueprint) return;
        unregister(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position - transform.forward * length / 2f,
                        transform.position + transform.forward * length / 2f);

        Gizmos.color = Color.red;
        foreach (var s in snap_points)
            Gizmos.DrawWireSphere(s.transform.position, SNAP_ATTACH_RANGE);

        foreach (var r in attached_to)
            Gizmos.DrawLine(transform.position, r.transform.position);
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<rail> rails = new HashSet<rail>();

    public static void register(rail r)
    {
        if (rails.Contains(r))
            throw new System.Exception("Rail already registered!");
        rails.Add(r);

        foreach (var r2 in rails)
            if (r.can_attach_to(r2))
            {
                r.attached_to.Add(r2);
                r2.attached_to.Add(r);
            }
    }

    public static void unregister(rail r)
    {
        rails.Remove(r);
        foreach (var r2 in r.attached_to)
            r2.attached_to.Remove(r);
        r.attached_to.Clear();
    }
}
