using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class match_rotations : MonoBehaviour
{
    public Transform target;
    public float multiplier = 1;
    public Transform y_axis_to_rotate_around;

    Quaternion last_target_rotation;

    private void Start()
    {
        last_target_rotation = target.rotation;
    }

    void Update()
    {
        Quaternion delta = target.rotation * Quaternion.Inverse(last_target_rotation);
        last_target_rotation = target.rotation;

        delta.ToAngleAxis(out float angle, out Vector3 rot_axis);
        angle *= Vector3.Dot(rot_axis, y_axis_to_rotate_around.up) * multiplier;

        transform.Rotate(y_axis_to_rotate_around.up, angle, Space.World);
    }
}