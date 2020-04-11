using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class path_test_point : MonoBehaviour
{
    public path_test_point next;
    Vector3 last_calculated_position;
    Vector3 last_calculated_end;
    path path = null;

    void calculate()
    {
        if (next == null) return;
        path = new path(transform.position, next.transform.position);
    }

    void Update()
    {
        calculate();
    }

    void OnDrawGizmos()
    {
        path?.draw_waypoint_gizmos(transform.position);
        path?.draw_gizmos();
    }
}