using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class impact_test_point : MonoBehaviour
{
    public Vector3 centre;
    public float length = 1f;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawLine(centre, centre + Vector3.forward * length);
    }

    public bool test(item i)
    {
        Vector3 from = transform.TransformPoint(centre);
        bool hit = false;
        foreach (var h in Physics.RaycastAll(from, transform.forward, length))
        {
            var aii = h.collider.GetComponent<accepts_item_impact>();

            if (aii != null)
                if (aii.on_impact(i))
                    return true;

            hit = true;
        }

        return hit;
    }
}

public class accepts_item_impact : MonoBehaviour
{
    public virtual bool on_impact(item i) { return false; }
}