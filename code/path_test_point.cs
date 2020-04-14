using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//[ExecuteInEditMode]
public class path_test_point : MonoBehaviour
{
    public path_test_point next;

    Vector3 last_calculated_position;
    Vector3 last_calculated_end;

    path path = null;

    void recalculate()
    {
        last_calculated_end = next.transform.position;
        last_calculated_position = transform.position;
        path = new path(transform.position, next.transform.position);
    }

    void Update()
    {
        if (next == null) return;

        if ((next.transform.position - last_calculated_end).magnitude > 0.1f ||
            (transform.position - last_calculated_position).magnitude > 0.1f)
            recalculate();

        path?.run_pathfinding(4);
    }

    void OnDrawGizmos()
    {
        path?.draw_gizmos();
    }
}