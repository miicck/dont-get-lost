using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class building_material : item
{
    //###########//
    // CONSTANTS //
    //###########//

    const float CARRY_RESTORE_FORCE = 10f;
    public const float WELD_RANGE = 5f;

    //#########//
    // WELDING //
    //#########//

    // Points that welds snap to
    snap_point[] snap_points { get { return GetComponentsInChildren<snap_point>(); } }

    // A weld represents the fixture of an item via a pivot
    // to a particular weld point in space
    public class weld_info
    {
        item to_weld;             // The item being welded
        snap_point pivot;         // The pivot of the item, in snapped orientation
        Vector3 weld_location;    // The world location of the weld
        Quaternion weld_rotation; // The rotation of the weld (specifying the snap directions)

        // Rotate the item so that the pivot has the given rotation
        void set_pivot_rotation(Quaternion rotation)
        {
            // Rotate
            to_weld.transform.rotation = rotation;
            to_weld.transform.rotation *= Quaternion.Inverse(pivot.transform.localRotation);

            // Align
            Vector3 disp = weld_location - pivot.transform.position;
            to_weld.transform.position += disp;
        }

        // Constructor
        public weld_info(
            item to_weld,
            snap_point pivot,
            Vector3 weld_location,
            Quaternion weld_rotation
            )
        {
            this.to_weld = to_weld;
            this.pivot = pivot;
            this.weld_location = weld_location;
            this.weld_rotation = weld_rotation;
            snap_pivot_rotation();
        }

        // The primary axes in the weld-rotated coordinate system
        Vector3[] weld_axes()
        {
            // Axes relative to weld axis
            Vector3[] axes = new Vector3[]
            {
                new Vector3( 1,0,0), new Vector3(0, 1,0), new Vector3(0,0, 1),
                new Vector3(-1,0,0), new Vector3(0,-1,0), new Vector3(0,0,-1)
            };

            // Rotate by the weld rotation/normalize to
            // obtain global rotation weld-axes
            for (int i = 0; i < axes.Length; ++i)
                axes[i] = weld_rotation * axes[i];

            return axes;
        }

        // The rotation axes to snap the pivot up-direction to
        Vector3[] snap_axes()
        {
            // Axes relative to weld axis
            Vector3[] axes = new Vector3[]
            {
                new Vector3( 1, 0, 0), new Vector3( 0, 1, 0), new Vector3( 0, 0, 1),
                new Vector3(-1, 0, 0), new Vector3( 0,-1, 0), new Vector3( 0, 0,-1),
                new Vector3( 0, 1, 1), new Vector3( 1, 0, 1), new Vector3( 1, 1, 0),
                new Vector3( 0,-1, 1), new Vector3(-1, 0, 1), new Vector3(-1, 1, 0),
                new Vector3( 0, 1,-1), new Vector3( 1, 0,-1), new Vector3( 1,-1, 0),
                new Vector3( 0,-1,-1), new Vector3(-1, 0,-1), new Vector3(-1,-1, 0),
            };

            // Rotate by the weld rotation/normalize to
            // obtain global rotation snap-axes
            for (int i = 0; i < axes.Length; ++i)
                axes[i] = weld_rotation * axes[i].normalized;

            return axes;
        }

        void snap_pivot_rotation()
        {
            // Set the pivot to point along the closest axes to it's current up/forward
            Vector3 up_axis = utils.find_to_min(snap_axes(), (a) => -Vector3.Dot(a, pivot.transform.up));
            Vector3 fw_axis = utils.find_to_min(snap_axes(), (a) => -Vector3.Dot(a, pivot.transform.forward));
            set_pivot_rotation(Quaternion.LookRotation(fw_axis, up_axis));
        }

        // Rotate the pivot with the keyboard keys
        public void key_rotate()
        {
            Vector3 rd = utils.find_to_min(weld_axes(), (a) => -Vector3.Dot(a, player.current.camera.transform.right));
            Vector3 fd = utils.find_to_min(weld_axes(), (a) => -Vector3.Dot(a, player.current.camera.transform.forward));
            Vector3 ud = utils.find_to_min(weld_axes(), (a) => -Vector3.Dot(a, player.current.camera.transform.up));

            if (Input.GetKeyDown(KeyCode.D)) to_weld.transform.RotateAround(pivot.transform.position, -fd, 50);
            else if (Input.GetKeyDown(KeyCode.A)) to_weld.transform.RotateAround(pivot.transform.position, fd, 50);
            else if (Input.GetKeyDown(KeyCode.S)) to_weld.transform.RotateAround(pivot.transform.position, -rd, 50);
            else if (Input.GetKeyDown(KeyCode.W)) to_weld.transform.RotateAround(pivot.transform.position, rd, 50);
            else if (Input.GetKeyDown(KeyCode.Q)) to_weld.transform.RotateAround(pivot.transform.position, -ud, 50);
            else if (Input.GetKeyDown(KeyCode.E)) to_weld.transform.RotateAround(pivot.transform.position, ud, 50);
            else return;

            snap_pivot_rotation();
        }

        public void draw_gizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(
                pivot.transform.position,
                pivot.transform.position + pivot.transform.up);

            Gizmos.color = Color.red;
            Vector3 d = Vector3.one / 100f;
            Gizmos.DrawLine(weld_location + d, weld_location + d + weld_rotation * Vector3.up);

            Gizmos.color = Color.cyan;
            foreach (var a in snap_axes())
                Gizmos.DrawLine(weld_location, weld_location + a / 2);
        }
    }

    // The current weld
    weld_info _weld;
    public weld_info weld
    {
        get { return _weld; }
        set
        {
            _weld = value;
            if (value == null)
            {
                rigidbody.isKinematic = false;
                return;
            }

            rigidbody.isKinematic = true;
        }
    }

    //##########//
    // ITEM USE //
    //##########//

    snap_point closest_to_ray(Ray ray)
    {
        snap_point ret = null;

        // Attempt to raycast to this item/find the nearest
        // snap_point to the raycast hit
        RaycastHit hit;
        if (utils.raycast_for_closest<item>(
            ray, out hit, WELD_RANGE,
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
            if (dis < min_dis)
            {
                min_dis = dis;
                ret = sp;
            }
        }

        return ret;
    }

    void spawn_from_inventory_and_fix_to(building_material other, RaycastHit hit)
    {
        var spawned = (building_material)spawn(name, hit.point, Quaternion.identity);
        snap_point snap_from = spawned.closest_to_ray(player.current.camera_ray());
        snap_point snap_to = other.closest_to_ray(player.current.camera_ray());

        if (snap_from == null || snap_to == null)
        {
            Destroy(spawned.gameObject);
            return;
        }

        player.current.inventory.remove(name, 1);

        spawned.weld = new weld_info(spawned,
            snap_from,
            snap_to.transform.position,
            Quaternion.identity);
    }

    void spawn_from_inventory_and_fix_at(RaycastHit hit)
    {
        var spawned = (building_material)spawn(name, hit.point, Quaternion.identity);
        snap_point snap_from = spawned.closest_to_ray(player.current.camera_ray());

        if (snap_from == null)
        {
            Destroy(spawned.gameObject);
            return;
        }

        spawned.weld = new weld_info(spawned,
            snap_from,
            hit.point,
            Quaternion.identity);
    }

    public override void use_left_click()
    {
        RaycastHit hit;
        var bm = utils.raycast_for_closest<building_material>(player.current.camera_ray(), out hit, WELD_RANGE);
        if (bm != null)
        {
            spawn_from_inventory_and_fix_to(bm, hit);
            return;
        }

        if (Physics.Raycast(player.current.camera_ray(), out hit, WELD_RANGE))
            spawn_from_inventory_and_fix_at(hit);
    }
}