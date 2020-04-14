using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class navmesh_test_point : MonoBehaviour
{
    public bool gen_path = false;
    public navmesh_test_point next;
    List<Vector3> path;

    void Update()
    {
        if (!gen_path) return;
        gen_path = false;

        if (next == null) return;

        path = procedural_navmesh.path(transform.position, next.transform.position);
    }

    void OnDrawGizmos()
    {
        if (path == null) return;
        Gizmos.color = Color.green;
        for (int i = 1; i < path.Count; ++i)
            Gizmos.DrawLine(path[i], path[i - 1]);
    }
}
