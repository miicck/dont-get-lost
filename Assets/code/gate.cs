using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class gate : MonoBehaviour
{
    public AXIS axis;
    public float closed_angle = 0f;
    public float open_angle = 90f;
    public float speed = 90f;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.axis(axis));
    }

    Quaternion closed_rotation;
    Quaternion open_rotation;

    private void Start()
    {
        closed_rotation = transform.rotation;
        open_rotation = Quaternion.AngleAxis(open_angle, transform.axis(axis)) * transform.rotation;
    }

    public bool open
    {
        get => _open || (player.current != null &&
            (player.current.transform.position - transform.position).magnitude < 3f);
        set => _open = value;
    }
    bool _open;

    void Update()
    {
        utils.rotate_towards(transform, open ? open_rotation : closed_rotation, Time.deltaTime * speed);
    }
}
