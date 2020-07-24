using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class railway_snap_point : snap_point
{
    public railway railway => GetComponentInParent<railway>();
    public bool outward;

    public railway_snap_point linked_to { get; private set; }

    public bool try_link(railway_snap_point other)
    {
        if (other.linked_to != null && other.linked_to != this) return false;
        if (outward == other.outward) return false;
        if ((other.transform.position - transform.position).magnitude > 0.1f)
            return false;

        linked_to = other;
        other.linked_to = this;
        return true;
    }

    public void unlink()
    {
        if (linked_to != null) linked_to.linked_to = null;
        linked_to = null;
        if (railway != null)
            railway.validate_links();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = outward ? Color.green : Color.red;
        if (linked_to != null)
            Gizmos.DrawWireSphere(transform.position, outward ? 0.2f : 0.1f);
        Gizmos.DrawLine(transform.position,
            transform.position + transform.forward);
    }
}
