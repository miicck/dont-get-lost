using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class building_material : item
{
    public const float BUILD_RANGE = 5f;
    public float axes_scale = 1f;

    //#########//
    // WELDING //
    //#########//

    // Points that welds snap to
    protected snap_point[] snap_points { get { return GetComponentsInChildren<snap_point>(); } }

    // A weld represents the fixture of an item via a pivot
    // to a particular weld point in space
    public class weld_info
    {
        building_material to_weld; // The item being welded
        snap_point pivot;          // The pivot of the item, in snapped orientation

        public Vector3 weld_location { get; private set; }    // The world location of the weld
        public Quaternion weld_rotation { get; private set; } // The rotation of the weld (specifying the snap directions)

        // The rotation of the rotation axes
        public Quaternion rotation_axes_rotation
        {
            get => Quaternion.LookRotation(forward_rot, up_rot);
        }

        /// <summary> The displayed axes. </summary>
        axes axes;

        /// <summary> Are the axes shown? </summary>
        public bool display_axes
        {
            get => axes != null;
            set
            {
                if (display_axes == value)
                    return; // No change

                if (value)
                {
                    axes = Resources.Load<axes>("misc/axes").inst();
                    axes.transform.localScale = Vector3.one * to_weld.axes_scale;
                }
                else
                    Destroy(axes.gameObject);
            }
        }

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

        /// <summary>
        /// The index of the pivot in <see cref="to_weld"/>.snap_points.
        /// </summary>
        int pivot_index
        {
            get => _pivot_index;
            set
            {
                _pivot_index = value;
                if (_pivot_index > to_weld.snap_points.Length - 1) _pivot_index = 0;
                else if (_pivot_index < 0) _pivot_index = to_weld.snap_points.Length - 1;
                pivot = to_weld.snap_points[_pivot_index];
            }
        }
        static int _pivot_index; // Static so it's remembered between placements

        // Constructor
        public weld_info(
            building_material to_weld,
            Vector3 weld_location,
            Quaternion weld_rotation
            )
        {
            this.to_weld = to_weld;
            pivot_index = pivot_index; // Loads the pivot

            this.weld_location = weld_location;
            this.weld_rotation = weld_rotation;

            // Determine the rotation axes
            right_rot = utils.find_to_min(possible_axes(), (a) => Vector3.Angle(a, player.current.transform.right));
            forward_rot = utils.find_to_min(possible_axes(), (a) => Vector3.Angle(a, player.current.transform.forward));
            up_rot = utils.find_to_min(possible_axes(), (a) => Vector3.Angle(a, player.current.transform.up));

            display_axes = true;
            axes.transform.position = weld_location;
            axes.transform.rotation = rotation_axes_rotation;

            set_pivot_rotation(rotation_axes_rotation);
        }

        // The possible rotation axes
        Vector3[] possible_axes()
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

        // Rotate the pivot with the keyboard keys
        public Vector3 right_rot { get; private set; }
        public Vector3 forward_rot { get; private set; }
        public Vector3 up_rot { get; private set; }

        public void key_rotate()
        {
            float pivot_change_dir = controls.get_axis("Mouse ScrollWheel");
            if (controls.key_press(controls.BIND.CHANGE_PIVOT)) pivot_change_dir = 1f;
            if (pivot_change_dir != 0)
            {
                // Change the pivot
                Quaternion saved_rotation = pivot.transform.rotation;
                pivot_index += pivot_change_dir > 0 ? 1 : -1;
                set_pivot_rotation(saved_rotation);
            }

            if (controls.key_down(controls.BIND.FINE_ROTATION))
            {
                // Continuous rotation
                if (controls.key_down(controls.BIND.ROTATE_ANTICLOCKWISE_AROUND_FORWARD))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, -forward_rot, Time.deltaTime * 15f);
                    axes.highlight_axis(axes.AXIS.Z);
                }
                else if (controls.key_down(controls.BIND.ROTATE_CLOCKWISE_AROUND_FORWARD))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, forward_rot, Time.deltaTime * 15f);
                    axes.highlight_axis(axes.AXIS.Z);
                }
                else if (controls.key_down(controls.BIND.ROTATE_ANTICLOCKWISE_AROUND_RIGHT))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, -right_rot, Time.deltaTime * 15f);
                    axes.highlight_axis(axes.AXIS.X);
                }
                else if (controls.key_down(controls.BIND.ROTATE_CLOCKWISE_AROUND_RIGHT))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, right_rot, Time.deltaTime * 15f);
                    axes.highlight_axis(axes.AXIS.X);
                }
                else if (controls.key_down(controls.BIND.ROTATE_ANTICLOCKWISE_AROUND_UP))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, -up_rot, Time.deltaTime * 15f);
                    axes.highlight_axis(axes.AXIS.Y);
                }
                else if (controls.key_down(controls.BIND.ROTATE_CLOCKWISE_AROUND_UP))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, up_rot, Time.deltaTime * 15f);
                    axes.highlight_axis(axes.AXIS.Y);
                }
            }

            // Rotation by 45 degree increments
            else if (controls.key_press(controls.BIND.ROTATE_ANTICLOCKWISE_AROUND_FORWARD))
            {
                to_weld.transform.RotateAround(pivot.transform.position, -forward_rot, 45);
                axes.highlight_axis(axes.AXIS.Z);
            }
            else if (controls.key_press(controls.BIND.ROTATE_CLOCKWISE_AROUND_FORWARD))
            {
                to_weld.transform.RotateAround(pivot.transform.position, forward_rot, 45);
                axes.highlight_axis(axes.AXIS.Z);
            }
            else if (controls.key_press(controls.BIND.ROTATE_ANTICLOCKWISE_AROUND_RIGHT))
            {
                to_weld.transform.RotateAround(pivot.transform.position, -right_rot, 45);
                axes.highlight_axis(axes.AXIS.X);
            }
            else if (controls.key_press(controls.BIND.ROTATE_CLOCKWISE_AROUND_RIGHT))
            {
                to_weld.transform.RotateAround(pivot.transform.position, right_rot, 45);
                axes.highlight_axis(axes.AXIS.X);
            }
            else if (controls.key_press(controls.BIND.ROTATE_ANTICLOCKWISE_AROUND_UP))
            {
                to_weld.transform.RotateAround(pivot.transform.position, -up_rot, 45);
                axes.highlight_axis(axes.AXIS.Y);
            }
            else if (controls.key_press(controls.BIND.ROTATE_CLOCKWISE_AROUND_UP))
            {
                to_weld.transform.RotateAround(pivot.transform.position, up_rot, 45);
                axes.highlight_axis(axes.AXIS.Y);
            }
            else return;
        }
    }

    // The current weld
    public weld_info weld { get; private set; }

    //##########//
    // ITEM USE //
    //##########//

    snap_point closest_to_ray(Ray ray, float ray_distance)
    {
        snap_point ret = null;

        // Attempt to raycast to this item/find the nearest
        // snap_point to the raycast hit
        RaycastHit hit;
        if (utils.raycast_for_closest<item>(
            ray, out hit, ray_distance,
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

    void make_blueprint()
    {
        // Create a blue, non-colliding placeholder version of this object
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.material = Resources.Load<Material>("materials/standard_shader/building_placeholder");
        foreach (var c in GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
            Destroy(c);
        }
    }

    building_material spawn_and_fix_to(
        building_material other, RaycastHit hit,
        Ray player_ray, float ray_distance)
    {
        var spawned = (building_material)create(name, hit.point, Quaternion.identity);
        spawned.make_blueprint();

        snap_point snap_to = other.closest_to_ray(player_ray, ray_distance);

        if (snap_to == null)
        {
            Destroy(spawned.gameObject);
            return null;
        }

        spawned.weld = new weld_info(spawned,
            snap_to.transform.position,
            snap_to.transform.rotation);

        return spawned;
    }

    building_material spawn_and_fix_at(RaycastHit hit)
    {
        var spawned = (building_material)create(name, hit.point, Quaternion.identity);
        spawned.make_blueprint();

        spawned.weld = new weld_info(spawned,
            hit.point,
            player.current.transform.rotation);

        return spawned;
    }

    building_material spawned;
    static float last_time_placing_blueprint;

    public override bool allow_right_click_held_down()
    {
        return true;
    }

    Ray camera_ray;
    public override use_result on_use_start(player.USE_TYPE use_type)
    {
        if (snap_points.Length == 0)
            throw new System.Exception("No snap points found on " + display_name + "!");

        // Get the ray to cast along, that stays within 
        // BUILD_RANGE of the player
        float raycast_distance = 0;
        camera_ray = player.current.camera_ray(BUILD_RANGE, out raycast_distance);

        if (use_type == player.USE_TYPE.USING_RIGHT_CLICK)
        {
            // Stop cancelling a build with right-click from immediately 
            // destroying the object we were welding to during the build.
            const float WAIT_AFTER_BUILD = 0.25f;
            if (Time.realtimeSinceStartup < last_time_placing_blueprint + WAIT_AFTER_BUILD)
                return use_result.complete;

            // Right click destroys items of the same kind
            RaycastHit same_hit;
            building_material found_same = utils.raycast_for_closest<building_material>(
                camera_ray, out same_hit, raycast_distance, (b) => b.name == name);
            if (found_same != null)
                found_same.pick_up();
            return use_result.complete;
        }

        // Only allow left click action from here
        if (use_type != player.USE_TYPE.USING_LEFT_CLICK)
            return use_result.complete;

        // Find a building_material/snap_point under cursor 
        // (unless ignore_snap_points is held)
        RaycastHit hit = default;
        building_material bm = null;
        if (!controls.key_down(controls.BIND.IGNORE_SNAP_POINTS))
            bm = utils.raycast_for_closest<building_material>(
                camera_ray, out hit, raycast_distance);

        // If a building material is found, fix new build to it
        // otherwise, just fix to any solid object
        if (bm != null)
            spawned = spawn_and_fix_to(bm, hit, camera_ray, raycast_distance);
        else
        {
            var col = utils.raycast_for_closest<Collider>(camera_ray, out hit, raycast_distance,
                (c) => !c.transform.IsChildOf(player.current.transform));

            if (col != null) spawned = spawn_and_fix_at(hit);
        }

        // Move onto rotation stage if something was spawned
        if (spawned != null) return use_result.underway_allows_look_only;
        return use_result.complete;
    }

    public override use_result on_use_continue(player.USE_TYPE use_type)
    {
        if (spawned == null || controls.mouse_click(controls.MOUSE_BUTTON.LEFT))
            return use_result.complete;

        if (controls.mouse_click(controls.MOUSE_BUTTON.RIGHT))
        {
            // Cancel build on right click
            spawned.weld.display_axes = false;
            Destroy(spawned.gameObject);
            spawned = null;
            return use_result.complete;
        }

        spawned.weld.key_rotate();
        last_time_placing_blueprint = Time.realtimeSinceStartup;
        return use_result.underway_allows_look_only;
    }

    /// <summary> Returns the networked object to parent this 
    /// building material to when it is placed. </summary>
    protected virtual networked parent_on_placement() { return null; }

    /// <summary> Called when the object is built. </summary>
    protected virtual void on_build() { }

    public override void on_use_end(player.USE_TYPE use_type)
    {
        if (spawned != null)
        {
            // Remove the object we're building from the inventory
            player.current.inventory.remove(spawned, 1);

            // Create a proper, networked version of the spawned object
            var created = (building_material)create(spawned.name,
                spawned.transform.position, spawned.transform.rotation,
                networked: true, network_parent: parent_on_placement());

            created.on_build();

            spawned.weld.display_axes = false;
            Destroy(spawned.gameObject);
        }

        spawned = null;
        player.current.validate_equip();
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawLine(camera_ray.origin, camera_ray.origin + camera_ray.direction);
    }
}