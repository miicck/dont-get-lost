using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class transform_path_follower : MonoBehaviour
{
    public float lerp_speed = 1f;
    public transform_path following;
    int path_index = 0;

    void Start()
    {
        if (following == null)
            return;

        // Find the path index we are closest to at the start
        float max_angle = 0;
        for (int i=1; i<following.waypoint_count; ++i)
        {
            Vector3 a = following.waypoint(i-1).position;
            Vector3 b = following.waypoint(i).position;

            Vector3 to_a = a - transform.position;
            Vector3 to_b = b - transform.position;

            float angle = Vector3.Angle(to_a, to_b);
            if (angle > max_angle)
            {
                max_angle = angle;
                path_index = i;
            }
        }
    }

    bool move_towards_next(ref float move_remaining)
    {
        Vector3 next = following.waypoint(path_index).position;
        Vector3 delta = next - transform.position;

        bool arrived = false;
        if (delta.magnitude > move_remaining)
            delta = delta.normalized * move_remaining;
        else arrived = true;

        transform.position += delta;
        move_remaining -= delta.magnitude;
        return arrived;
    }

    void Update()
    {
        if (following == null) return;

        float to_move = Time.deltaTime;
        while(move_towards_next(ref to_move))
           path_index = (path_index + 1) % following.waypoint_count;

        transform.rotation = Quaternion.Lerp(transform.rotation,
            following.waypoint(path_index).rotation, Time.deltaTime * lerp_speed);
    }
}
