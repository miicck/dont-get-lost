using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item : interactable
{
    const float CARRY_RESTORE_FORCE = 10f;

    new public Rigidbody rigidbody { get; private set; }

    public static item spawn(string name, Vector3 position)
    {
        var i = Resources.Load<item>("items/" + name).inst();
        i.transform.position = position;
        i.rigidbody = i.gameObject.AddComponent<Rigidbody>();
        i.rigidbody.velocity = Random.onUnitSphere;
        return i;
    }

    private void Start()
    {
        transform.Rotate(0, Random.Range(0, 360), 0);
    }

    snap_point[] snap_points { get { return GetComponentsInChildren<snap_point>(); } }

    snap_point closest_to_ray(Ray ray)
    {
        snap_point ret = null;

        // Attempt to raycast to this item/find the nearest
        // snap_point to the raycast hit
        RaycastHit hit;
        if (utils.raycast_for_closest<item>(
            ray, out hit, player.INTERACTION_RANGE, 
            (t) => t == this))
        {
            // Find the nearest snap point to the hit
            float min_dis_pt = float.MaxValue;
            foreach (var s in snap_points)
            {
                float dis_pt = (s.transform.position - hit.point).sqrMagnitude;
                if (dis_pt < min_dis_pt)
                {
                    min_dis_pt = dis_pt;
                    ret = s;
                }
            }
        }

        if (ret != null)
            return ret;

        // Just find the nearest snap point to the ray
        float min_dis = float.MaxValue;
        foreach (var sp in snap_points)
        {
            Vector3 to_line = sp.transform.position - ray.origin;
            to_line -= Vector3.Project(to_line, ray.direction);
            float dis = to_line.sqrMagnitude;
            if (dis  < min_dis)
            {
                min_dis = dis;
                ret = sp;
            }
        }

        return ret;
    }

    Vector3 last_snapped_position;
    void fix_to(item other)
    {
        snap_point snap_from = this.closest_to_ray(player.current.camera_ray());
        snap_point snap_to = other.closest_to_ray(player.current.camera_ray());

        if (snap_from == null) return;
        if (snap_to == null) return;

        transform.position += (snap_to.transform.position - snap_from.transform.position);

        rigidbody.isKinematic = true;
        last_snapped_position = transform.position;
    }

    public override void player_interact()
    {
        // Drop item
        if (Input.GetMouseButtonDown(0))
        {
            stop_interaction();
            return;
        }

        // Unweld if moved far enough from snap position
        if (rigidbody.isKinematic)
        {
            Vector3 disp = last_snapped_position - transform.position;
            if (disp.magnitude > 0.1f)
                rigidbody.isKinematic = false;
        }

        if (Input.GetMouseButtonDown(1))
        {
            RaycastHit hit;
            item other = utils.raycast_for_closest<item>(
                player.current.camera_ray(), out hit,
                player.INTERACTION_RANGE, (t) => t != this);

            if (other != null)
                fix_to(other);
        }

        Vector3 carry_point = player.current.camera.transform.position +
            carry_distance * player.current.camera.transform.forward;

        Vector3 dx = carry_pivot.position - carry_point;
        Vector3 v = rigidbody.velocity;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0) carry_distance *= 1.2f;
        else if (scroll < 0) carry_distance /= 1.2f;
        carry_distance = Mathf.Clamp(carry_distance, 1.0f, player.INTERACTION_RANGE);

        rigidbody.AddForce(-CARRY_RESTORE_FORCE * (dx + v));
    }

    Transform carry_pivot;
    float carry_distance;

    public override void on_start_interaction(RaycastHit point_hit)
    {
        carry_pivot = new GameObject("pivot").transform;
        carry_pivot.SetParent(transform);
        carry_pivot.transform.position = point_hit.point;
        carry_pivot.rotation = player.current.camera.transform.rotation;

        transform.SetParent(player.current.camera.transform);
        rigidbody.isKinematic = false;
        rigidbody.useGravity = false;
        rigidbody.angularDrag *= 200f;
        carry_distance = 2f;
    }

    public override void on_end_interaction()
    {
        transform.SetParent(null);
        rigidbody.useGravity = true;
        rigidbody.angularDrag /= 200f;
        Destroy(carry_pivot.gameObject);
    }

    // Return a cursor that looks like grabbing if we are carrying an item
    public override string cursor()
    {
        return cursors.GRAB_CLOSED;
    }
}