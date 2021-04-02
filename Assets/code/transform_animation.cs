using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class transform_animation : MonoBehaviour
{
    public Transform target;

    Vector3 initial_local_pos;
    Quaternion initial_local_rot;

    Vector3 target_local_pos;
    Quaternion target_local_rot;

    private void Start()
    {
        // Record the initial and target local positions/rotations
        initial_local_pos = transform.localPosition;
        initial_local_rot = transform.localRotation;

        if (transform.parent == null)
        {
            target_local_pos = target.transform.position;
            target_local_rot = target.transform.rotation;
        }
        else
        {
            target_local_pos = transform.parent.InverseTransformPoint(target.transform.position);
            target_local_rot = Quaternion.Inverse(transform.parent.rotation) * target.transform.rotation;
        }
    }

    /// <summary> How far along the animation are we in [0, 1]. Values outside this range
    /// will be wrapped back into this range such that 1.2 -> 0.2 etc. </summary>
    public float progress
    {
        get => _progress;
        set
        {
            if (value > 1 || value < 0)
                value -= Mathf.Floor(value); // Wrap value into [0, 1]
            transform.localPosition = Vector3.Lerp(initial_local_pos, target_local_pos, value);
            transform.localRotation = Quaternion.Lerp(initial_local_rot, target_local_rot, value);
            _progress = value;
        }
    }
    float _progress = 0;
}
