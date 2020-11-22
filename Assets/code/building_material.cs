using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface INonBlueprintable { }

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

        public void on_finish()
        {
            Destroy(axes.gameObject);
            Destroy(rotation_axes.gameObject);
        }

        /// <summary> The displayed axes. </summary>
        axes axes;
        axes rotation_axes;

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
            Quaternion weld_rotation)
        {
            this.to_weld = to_weld;
            pivot_index = pivot_index; // Loads the pivot

            this.weld_location = weld_location;
            this.weld_rotation = weld_rotation;

            // Determine the rotation axes
            up_rot = utils.find_to_min(possible_axes(), (a) => Vector3.Angle(a, player.current.transform.up));
            right_rot = utils.find_to_min(possible_axes(), (a) => Vector3.Angle(a, player.current.transform.right));
            forward_rot = utils.find_to_min(possible_axes(), (a) => Vector3.Angle(a, player.current.transform.forward));

            // Create the axes/rotation axes
            axes = Resources.Load<axes>("misc/axes").inst();
            axes.transform.position = weld_location;
            axes.transform.rotation = rotation_axes_rotation;
            rotation_axes = Resources.Load<axes>("misc/rotation_axes").inst();
            rotation_axes.transform.position = weld_location;
            rotation_axes.transform.rotation = rotation_axes_rotation;

            // Axes start disabled if we're using key based building
            // (enabled if we're using mouse-based building)
            axes.gameObject.SetActive(!controls.key_based_building);
            rotation_axes.gameObject.SetActive(!controls.key_based_building);

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

        void translate(Vector3 amount)
        {
            to_weld.transform.position += amount;
            axes.transform.position += amount;
            rotation_axes.transform.position += amount;
        }

        enum MOUSE_MODE
        {
            NONE,
            X_TRANSLATE,
            X_ROTATE,
            Y_TRANSLATE,
            Y_ROTATE,
            Z_TRANSLATE,
            Z_ROTATE
        }
        MOUSE_MODE mouse_mode = MOUSE_MODE.NONE;

        Vector3? last_closest_point = null;
        float last_click_time = Time.realtimeSinceStartup;
        float accumulated_rotation = 0;

        void change_pivot()
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
        }

        public bool mouse_rotate()
        {
            change_pivot();

            if (controls.mouse_click(controls.MOUSE_BUTTON.LEFT))
            {
                if (Time.realtimeSinceStartup - last_click_time < 0.5f)
                    return true;

                last_click_time = Time.realtimeSinceStartup;
                var ray = player.current.camera_ray();
                mouse_mode = MOUSE_MODE.NONE;

                var trans = utils.raycast_for_closest<Transform>(ray, out RaycastHit hit,
                    accept: (t) => t.IsChildOf(axes.transform) || t.IsChildOf(rotation_axes.transform));

                if (trans != null)
                {
                    if (trans.IsChildOf(axes.transform))
                    {
                        if (trans.name.Contains("x")) mouse_mode = MOUSE_MODE.X_TRANSLATE;
                        else if (trans.name.Contains("y")) mouse_mode = MOUSE_MODE.Y_TRANSLATE;
                        else if (trans.name.Contains("z")) mouse_mode = MOUSE_MODE.Z_TRANSLATE;
                    }
                    else if (trans.IsChildOf(rotation_axes.transform))
                    {
                        if (trans.name.Contains("x")) mouse_mode = MOUSE_MODE.X_ROTATE;
                        else if (trans.name.Contains("y")) mouse_mode = MOUSE_MODE.Y_ROTATE;
                        else if (trans.name.Contains("z")) mouse_mode = MOUSE_MODE.Z_ROTATE;
                    }
                }
            }
            else if (controls.mouse_unclick(controls.MOUSE_BUTTON.LEFT))
            {
                last_closest_point = null;
                mouse_mode = MOUSE_MODE.NONE;
            }

            Vector3? new_closest_point = null;
            Vector3? rotation_axis = null;
            accumulated_rotation += 10 * controls.object_rotation_axis();

            switch (mouse_mode)
            {
                case MOUSE_MODE.X_TRANSLATE:
                    new_closest_point = utils.nearest_point_on_line_to_player_ray(new Ray(pivot.transform.position, right_rot));
                    axes.highlight_axis(axes.AXIS.X);
                    break;

                case MOUSE_MODE.Y_TRANSLATE:
                    new_closest_point = utils.nearest_point_on_line_to_player_ray(new Ray(pivot.transform.position, up_rot));
                    axes.highlight_axis(axes.AXIS.Y);
                    break;

                case MOUSE_MODE.Z_TRANSLATE:
                    new_closest_point = utils.nearest_point_on_line_to_player_ray(new Ray(pivot.transform.position, forward_rot));
                    axes.highlight_axis(axes.AXIS.Z);
                    break;

                case MOUSE_MODE.X_ROTATE:
                    rotation_axis = right_rot;
                    rotation_axes.highlight_axis(axes.AXIS.X);
                    break;

                case MOUSE_MODE.Y_ROTATE:
                    rotation_axis = -up_rot;
                    rotation_axes.highlight_axis(axes.AXIS.Y);

                    break;

                case MOUSE_MODE.Z_ROTATE:
                    rotation_axis = -forward_rot;
                    rotation_axes.highlight_axis(axes.AXIS.Z);
                    break;

                case MOUSE_MODE.NONE:
                    axes.highlight_axis(axes.AXIS.NONE);
                    rotation_axes.highlight_axis(axes.AXIS.NONE);
                    accumulated_rotation = 0;
                    break;
            }

            if (rotation_axis != null)
            {
                Vector3 rot_axis = (Vector3)rotation_axis;

                if (controls.key_down(controls.BIND.FINE_ROTATION))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, rot_axis, accumulated_rotation);
                    accumulated_rotation = 0;
                }
                else if (accumulated_rotation > 45f)
                {
                    to_weld.transform.RotateAround(pivot.transform.position, rot_axis, 45);
                    accumulated_rotation = 0;
                }
                else if (accumulated_rotation < -45f)
                {
                    to_weld.transform.RotateAround(pivot.transform.position, rot_axis, -45);
                    accumulated_rotation = 0;
                }
            }

            if (last_closest_point == null)
                last_closest_point = new_closest_point;
            else if (new_closest_point != null)
            {
                Vector3 delta = (Vector3)new_closest_point - (Vector3)last_closest_point;
                translate(delta);
                last_closest_point = new_closest_point;
            }

            return false;
        }

        public void key_rotate()
        {
            change_pivot();

            axes.gameObject.SetActive(controls.key_down(controls.BIND.BUILDING_TRANSLATION));
            rotation_axes.gameObject.SetActive(!controls.key_down(controls.BIND.BUILDING_TRANSLATION));

            if (controls.key_down(controls.BIND.BUILDING_TRANSLATION))
            {
                // Translate, rather than rotate
                float t_amount = Time.deltaTime / 2f;
                if (controls.key_down(controls.BIND.TRANSLATE_RIGHT)) translate(right_rot * t_amount);
                else if (controls.key_down(controls.BIND.TRANSLATE_LEFT)) translate(-right_rot * t_amount);
                else if (controls.key_down(controls.BIND.TRANSLATE_FORWARD)) translate(forward_rot * t_amount);
                else if (controls.key_down(controls.BIND.TRANSLATE_BACK)) translate(-forward_rot * t_amount);
                else if (controls.key_down(controls.BIND.TRANSLATE_UP)) translate(up_rot * t_amount);
                else if (controls.key_down(controls.BIND.TRANSLATE_DOWN)) translate(-up_rot * t_amount);
            }

            else if (controls.key_down(controls.BIND.FINE_ROTATION))
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

    public bool is_blueprint { get; private set; }

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

    public override void on_equip(bool local_player)
    {
        base.on_equip(local_player);

        if (local_player)
        {
            // Draw links if a path element is equipped
            if (GetComponentInChildren<settler_path_element>() != null)
                settler_path_element.draw_links = true;
        }
    }

    public override void on_unequip(bool local_player)
    {
        base.on_unequip(local_player);

        if (local_player)
        {
            // Stop drawing links if a path element is unequipped
            if (GetComponentInChildren<settler_path_element>() != null)
                settler_path_element.draw_links = false;
        }
    }

    void make_blueprint()
    {
        // Create a blue, placeholder version of this object
        foreach (var comp in GetComponentsInChildren<Component>())
        {
            // Replace all materials with the building placeholder material
            if (comp is Renderer)
            {
                var rend = (Renderer)comp;
                rend.material = Resources.Load<Material>("materials/standard_shader/building_placeholder");
                continue;
            }

            // Highlight the snap points
            if (comp is snap_point)
            {
                var sp = (snap_point)comp;
                var hl = Resources.Load<GameObject>("misc/snap_point_highlight").inst();
                hl.transform.SetParent(sp.transform);
                hl.transform.localPosition = Vector3.zero;
                hl.transform.localRotation = Quaternion.identity;
            }

            // Destroy colliders
            if (comp is Collider)
            {
                Destroy(comp);
                continue;
            }

            // Remove componenets tagged as INonBlueprintable
            if (comp is INonBlueprintable)
                Destroy(comp);
        }

        // Don't carry out normal updates on blueprinted version.
        enabled = false;
        is_blueprint = true;
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
            if (found_same != null) found_same.pick_up(register_undo: true);
            return use_result.complete;
        }

        // Only allow left click action from here
        if (use_type != player.USE_TYPE.USING_LEFT_CLICK)
            return use_result.complete;

        // Find a (non-logistics version) building_material/snap_point 
        // under cursor (unless ignore_snap_points is held)
        RaycastHit hit = default;
        building_material bm = null;
        if (!controls.key_down(controls.BIND.IGNORE_SNAP_POINTS))
            bm = utils.raycast_for_closest<building_material>(
                camera_ray, out hit, raycast_distance, accept: (b) => !b.is_logistics_version);

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
        if (spawned != null)
        {
            if (!controls.key_based_building) player.current.cursor_sprite = cursors.DEFAULT;
            return use_result.underway_allows_look_only;
        }
        return use_result.complete;
    }

    public override use_result on_use_continue(player.USE_TYPE use_type)
    {
        if (spawned == null)
            return use_result.complete;

        last_time_placing_blueprint = Time.realtimeSinceStartup;

        if (controls.mouse_click(controls.MOUSE_BUTTON.RIGHT))
        {
            // Cancel build on right click
            spawned.weld.on_finish();
            Destroy(spawned.gameObject);
            spawned = null;
            return use_result.complete;
        }

        if (controls.key_based_building)
        {
            if (controls.mouse_click(controls.MOUSE_BUTTON.LEFT))
                return use_result.complete;
            spawned.weld.key_rotate();
            return use_result.underway_allows_look_only;
        }
        else
        {
            if (spawned.weld.mouse_rotate())
                return use_result.complete;
            return use_result.underway_allows_look_only;
        }
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
            if (player.current.inventory.remove(spawned, 1))
            {
                // Create a proper, networked version of the spawned object
                var created = (building_material)create(spawned.name,
                    spawned.transform.position, spawned.transform.rotation,
                    networked: true, network_parent: parent_on_placement(),
                    register_undo: true);

                created.on_build();
                spawned.weld.on_finish();
                Destroy(spawned.gameObject);
            }
        }

        spawned = null;
        player.current.validate_equip();
    }
}

public abstract class building_with_inventory : building_material
{
    public inventory inventory { get; private set; }

    protected override bool can_pick_up(out string message)
    {
        if (!inventory.empty)
        {
            message = "Inventory not empty!";
            return false;
        }

        return base.can_pick_up(out message);
    }

    protected abstract string inventory_prefab();
    protected virtual void on_set_inventory() { }

    public override void on_first_register()
    {
        base.on_first_register();
        client.create(transform.position, inventory_prefab(), this);
    }

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);
        if (child is inventory)
        {
            inventory = (inventory)child;
            on_set_inventory();
        }
    }
}