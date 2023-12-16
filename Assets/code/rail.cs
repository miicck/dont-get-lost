using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class rail : MonoBehaviour
{
    /// <summary> The building snap points at the ends of this rail. </summary>
    snap_point[] snap_points => GetComponentsInChildren<snap_point>();

    /// <summary> The end-to-end length of this rail. </summary>
    public float length
    {
        get
        {
            var cp = rail_overlap_capsule_points;
            return (cp[1] - cp[0]).magnitude;
        }
    }

    /// <summary> Capsule points used to determine rail overlap. </summary>
    Vector3[] rail_overlap_capsule_points
    {
        get
        {
            var my_snap_points = snap_points;
            if (my_snap_points.Length != 2)
            {
                Debug.LogError("Rail found with != 2 snap points!");
                return new Vector3[] { transform.position, transform.position };
            }

            return new Vector3[] { my_snap_points[0].transform.position, my_snap_points[1].transform.position };
        }
    }

    /// <summary> The radius of the capsule used to determine rail overlap. </summary>
    public const float RAIL_OVERLAP_CAPSULE_RADIUS = 0.2f;

    /// <summary> Returns true if this rail can connect 
    /// to the <paramref name="other"/> rail. </summary>
    bool can_attach_to(rail other)
    {
        if (this == null || other == null || other == this) return false;
        var cp = rail_overlap_capsule_points;
        foreach (var c in Physics.OverlapCapsule(cp[0], cp[1], RAIL_OVERLAP_CAPSULE_RADIUS))
            if (c.GetComponentInParent<rail>() == other)
                return true;
        return false;
    }

    /// <summary> The other rails that I am attached to. </summary>
    IEnumerable<rail> attached_to => rails.Where(can_attach_to);

    /// <summary> Returns a number that represents how 
    /// nicely we can transition to the given next rail. 
    /// Will return Mathf.Infinity if transition is impossible.</summary>
    float transition_score(rail next, Vector3 direction)
    {
        float angle = Vector3.Angle(direction, next.transform.position - transform.position);
        if (angle > 91f) return Mathf.Infinity;
        return angle;
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
    public rail next(Vector3 direction) => utils.find_to_min(attached_to, (n) => transition_score(n, direction));

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
        foreach (var p in rail_overlap_capsule_points)
            Gizmos.DrawWireSphere(p, RAIL_OVERLAP_CAPSULE_RADIUS);

        foreach (var r in attached_to)
        {
            if (player.current != null)
                Gizmos.color = transition_score(r, player.current.eye_transform.forward)
                    < Mathf.Infinity ? Color.green : Color.red;

            Vector3 delta = r.transform.position - transform.position;
            if (delta.z > 0)
                delta = Vector3.up / 10f;
            else
                delta = -Vector3.up / 10f;

            Gizmos.DrawLine(transform.position + delta, r.transform.position + delta);
        }
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
    }

    public static void unregister(rail r)
    {
        rails.Remove(r);
    }
}
