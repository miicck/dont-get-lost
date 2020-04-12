using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class procedural_navmesh_obstacle : MonoBehaviour
{
    public Collider target;
    public Bounds bounds { get { return target.bounds; } }
    float move_needed = 0.01f;

    Vector3 last_pos;
    void Start()
    {
        last_pos = transform.position;

    }

    void on_move()
    {
        foreach (var nm in procedural_navmesh.meshes)
            if (nm.bounds.Intersects(bounds))
            {
                if (nm.resolution / 2f < move_needed) move_needed = nm.resolution / 2f;
                nm.on_obstacle_move(this, last_pos, transform.position);
            }
    }

    void Update()
    {
        Vector3 delta = transform.position - last_pos;
        if (delta.magnitude > move_needed) on_move();
        last_pos = transform.position;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
