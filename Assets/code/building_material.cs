using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface INonBlueprintable { }

public interface IBuildListener
{
    void on_first_built();
}

public class building_material : item, IPlayerInteractable
{
    public const float BUILD_RANGE = 5f;
    public float axes_scale = 1f;

    static building_material()
    {
        tips.add("When placing an object, you can cycle through the " +
            "different starting orientations by pressing " +
            controls.bind_name(controls.BIND.INCREMENT_PIVOT) + " or by scrolling. " +
            "This initial orientation will be saved when placing subsequent objects.");

        tips.add("With a building material equipped, right click" +
            " to quickly delete objects of the same type.");

        tips.add("Building materials can be deleted by right-clicking on them with an empty hand. " +
            "Press " + controls.bind_name(controls.BIND.QUICKBAR_1) +
            " a few times to de-equip what you are holding.");

        tips.add("To turn off snapping when placing a building, hold " +
            controls.bind_name(controls.BIND.IGNORE_SNAP_POINTS) + ". " +
            "This will also orient the building to the world " +
            "axes, rather than to the parent building.");
    }

    //############//
    // NETWORKING //
    //############//

    public override void on_first_create()
    {
        base.on_first_create();
        foreach (var bl in GetComponentsInChildren<IBuildListener>())
            bl.on_first_built();
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    class pick_up_from_gutter : player_interaction
    {
        building_material material;
        public pick_up_from_gutter(building_material material) { this.material = material; }

        public override controls.BIND keybind => controls.BIND.USE_ITEM;
        public override bool is_possible()
        {
            return material.is_logistics_version;
        }

        public override bool start_interaction(player player)
        {
            material.pick_up();
            return true;
        }

        public override string context_tip()
        {
            return "pick up " + material.display_name;
        }
    }

    class deconstruct_interaction : player_interaction
    {
        building_material material;
        public deconstruct_interaction(building_material material) { this.material = material; }

        public override controls.BIND keybind => controls.BIND.ALT_USE_ITEM;
        public override bool is_possible() { return !material.is_logistics_version; }

        public override bool start_interaction(player player)
        {
            material.pick_up(true);
            player.current.play_sound("sounds/hammer_wood_lowpass", 0.9f, 1.1f, 0.5f, location: material.transform.position);
            return true;
        }

        public override string context_tip()
        {
            return "demolish " + material.display_name;
        }
    }

    public override player_interaction[] player_interactions()
    {
        return new player_interaction[]
        {
            new pick_up_from_gutter(this),
            new deconstruct_interaction(this),
            new select_matching_interaction(this),
            new player_inspectable(transform)
            {
                text = () => display_name + " (built)",
                sprite = () => sprite
            }
        };
    }

    //#########//
    // WELDING //
    //#########//

    // Points that welds snap to
    protected snap_point[] snap_points => GetComponentsInChildren<snap_point>();

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
            get
            {
                if (_pivot_index.ContainsKey(to_weld.name))
                    return _pivot_index[to_weld.name];
                return 0;
            }
            set
            {
                if (value > to_weld.snap_points.Length - 1) value = 0;
                else if (value < 0) value = to_weld.snap_points.Length - 1;
                _pivot_index[to_weld.name] = value;
                pivot = to_weld.snap_points[value];
            }
        }

        // Pivot index, by item name. Static so it's remembered between placements.
        static Dictionary<string, int> _pivot_index = new Dictionary<string, int>();

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
            axes.transform.localScale = Vector3.one * to_weld.axes_scale;
            rotation_axes = Resources.Load<axes>("misc/rotation_axes").inst();
            rotation_axes.transform.position = weld_location;
            rotation_axes.transform.rotation = rotation_axes_rotation;
            rotation_axes.transform.localScale = Vector3.one * to_weld.axes_scale;

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
            weld_location += amount;
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
            float pivot_change_dir = controls.delta(controls.BIND.CHANGE_PIVOT);
            if (controls.triggered(controls.BIND.INCREMENT_PIVOT)) pivot_change_dir = 1f;
            if (pivot_change_dir != 0)
            {
                // Change the pivot
                Quaternion saved_rotation = pivot.transform.rotation;
                pivot_index += pivot_change_dir > 0 ? 1 : -1;
                set_pivot_rotation(saved_rotation);
                player.current.play_sound("sounds/adjustment_click", 1f, 1f, 0.5f, location: weld_location, min_time_since_last: 0.05f);
            }
        }

        float adjustment_sound_accumulated = 1;
        float adjustment_sound_last_played = 0;

        /// <summary> Rotate the building using the mouse. Returns true 
        /// when the orientation is confirmed. </summary>
        public bool mouse_rotate()
        {
            change_pivot();

            if (controls.triggered(controls.BIND.USE_ITEM))
            {
                // Check for double-click to place
                if (Time.realtimeSinceStartup - last_click_time < 0.5f)
                    return true;

                last_click_time = Time.realtimeSinceStartup;

                var ray = player.current.camera_ray();
                mouse_mode = MOUSE_MODE.NONE;

                var trans = utils.raycast_for_closest<Transform>(ray, out RaycastHit hit,
                    accept: (h, t) => t.IsChildOf(axes.transform) || t.IsChildOf(rotation_axes.transform));

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
            else if (controls.untriggered(controls.BIND.USE_ITEM))
            {
                last_closest_point = null;
                mouse_mode = MOUSE_MODE.NONE;
            }

            Vector3? new_closest_point = null;
            Vector3? rotation_axis = null;
            float delta_rot_control = 10 * (controls.delta(controls.BIND.ROTATION_AMOUNT_X) +
                                            controls.delta(controls.BIND.ROTATION_AMOUNT_Y));
            accumulated_rotation += delta_rot_control;

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

                if (controls.held(controls.BIND.FINE_ROTATION))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, rot_axis, accumulated_rotation);
                    adjustment_sound_accumulated += Mathf.Abs(delta_rot_control) / 15f;
                    accumulated_rotation = 0;
                }
                else if (accumulated_rotation > 20f)
                {
                    to_weld.transform.RotateAround(pivot.transform.position, rot_axis, 45);
                    adjustment_sound_accumulated += 1.01f;
                    accumulated_rotation = 0;
                }
                else if (accumulated_rotation < -20f)
                {
                    to_weld.transform.RotateAround(pivot.transform.position, rot_axis, -45);
                    adjustment_sound_accumulated += 1.01f;
                    accumulated_rotation = 0;
                }
            }

            if (last_closest_point == null)
                last_closest_point = new_closest_point;
            else if (new_closest_point != null)
            {
                Vector3 delta = (Vector3)new_closest_point - (Vector3)last_closest_point;
                translate(delta);
                adjustment_sound_accumulated += delta.magnitude / 0.1f;
                last_closest_point = new_closest_point;
            }

            if (!controls.held(controls.BIND.USE_ITEM))
            {
                adjustment_sound_last_played = 0f;
                adjustment_sound_accumulated = 1f;
            }

            if (adjustment_sound_accumulated > adjustment_sound_last_played + 1f)
            {
                adjustment_sound_last_played = adjustment_sound_accumulated;
                float x = Mathf.Max(adjustment_sound_accumulated - 1f, 0f);
                float pitch = 1.5f - 0.5f * Mathf.Exp(-x / 10f);

                player.current.play_sound("sounds/adjustment_click", pitch, pitch, 0.5f,
                    location: weld_location, min_time_since_last: 0.05f);
            }

            return false;
        }

        public void key_rotate()
        {
            change_pivot();

            axes.gameObject.SetActive(controls.held(controls.BIND.BUILDING_TRANSLATION));
            rotation_axes.gameObject.SetActive(!controls.held(controls.BIND.BUILDING_TRANSLATION));

            if (controls.held(controls.BIND.BUILDING_TRANSLATION))
            {
                // Translate, rather than rotate
                float t_amount = Time.deltaTime / 2f;
                if (controls.held(controls.BIND.MANIPULATE_BUILDING_RIGHT))
                {
                    axes.highlight_axis(axes.AXIS.X);
                    translate(right_rot * t_amount);
                }
                else if (controls.held(controls.BIND.MANIPULATE_BUILDING_LEFT))
                {
                    axes.highlight_axis(axes.AXIS.X);
                    translate(-right_rot * t_amount);
                }
                else if (controls.held(controls.BIND.MANIPULATE_BUILDING_FORWARD))
                {
                    axes.highlight_axis(axes.AXIS.Z);
                    translate(forward_rot * t_amount);
                }
                else if (controls.held(controls.BIND.MANIPULATE_BUILDING_BACK))
                {
                    axes.highlight_axis(axes.AXIS.Z);
                    translate(-forward_rot * t_amount);
                }
                else if (controls.held(controls.BIND.MANIPULATE_BUILDING_UP))
                {
                    axes.highlight_axis(axes.AXIS.Y);
                    translate(up_rot * t_amount);
                }
                else if (controls.held(controls.BIND.MANIPULATE_BUILDING_DOWN))
                {
                    axes.highlight_axis(axes.AXIS.Y);
                    translate(-up_rot * t_amount);
                }
            }

            else if (controls.held(controls.BIND.FINE_ROTATION))
            {
                // Continuous rotation
                if (controls.held(controls.BIND.MANIPULATE_BUILDING_RIGHT))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, -forward_rot, Time.deltaTime * 15f);
                    rotation_axes.highlight_axis(axes.AXIS.Z);
                }
                else if (controls.held(controls.BIND.MANIPULATE_BUILDING_LEFT))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, forward_rot, Time.deltaTime * 15f);
                    rotation_axes.highlight_axis(axes.AXIS.Z);
                }
                else if (controls.held(controls.BIND.MANIPULATE_BUILDING_BACK))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, -right_rot, Time.deltaTime * 15f);
                    rotation_axes.highlight_axis(axes.AXIS.X);
                }
                else if (controls.held(controls.BIND.MANIPULATE_BUILDING_FORWARD))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, right_rot, Time.deltaTime * 15f);
                    rotation_axes.highlight_axis(axes.AXIS.X);
                }
                else if (controls.held(controls.BIND.MANIPULATE_BUILDING_DOWN))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, -up_rot, Time.deltaTime * 15f);
                    rotation_axes.highlight_axis(axes.AXIS.Y);
                }
                else if (controls.held(controls.BIND.MANIPULATE_BUILDING_UP))
                {
                    to_weld.transform.RotateAround(pivot.transform.position, up_rot, Time.deltaTime * 15f);
                    rotation_axes.highlight_axis(axes.AXIS.Y);
                }
            }

            // Rotation by 45 degree increments
            else if (controls.triggered(controls.BIND.MANIPULATE_BUILDING_RIGHT))
            {
                to_weld.transform.RotateAround(pivot.transform.position, -forward_rot, 45);
                rotation_axes.highlight_axis(axes.AXIS.Z);
            }
            else if (controls.triggered(controls.BIND.MANIPULATE_BUILDING_LEFT))
            {
                to_weld.transform.RotateAround(pivot.transform.position, forward_rot, 45);
                rotation_axes.highlight_axis(axes.AXIS.Z);
            }
            else if (controls.triggered(controls.BIND.MANIPULATE_BUILDING_BACK))
            {
                to_weld.transform.RotateAround(pivot.transform.position, -right_rot, 45);
                rotation_axes.highlight_axis(axes.AXIS.X);
            }
            else if (controls.triggered(controls.BIND.MANIPULATE_BUILDING_FORWARD))
            {
                to_weld.transform.RotateAround(pivot.transform.position, right_rot, 45);
                rotation_axes.highlight_axis(axes.AXIS.X);
            }
            else if (controls.triggered(controls.BIND.MANIPULATE_BUILDING_DOWN))
            {
                to_weld.transform.RotateAround(pivot.transform.position, -up_rot, 45);
                rotation_axes.highlight_axis(axes.AXIS.Y);
            }
            else if (controls.triggered(controls.BIND.MANIPULATE_BUILDING_UP))
            {
                to_weld.transform.RotateAround(pivot.transform.position, up_rot, 45);
                rotation_axes.highlight_axis(axes.AXIS.Y);
            }
            else return;
        }
    }

    // The current weld
    public weld_info weld { get; private set; }

    //##########//
    // ITEM USE //
    //##########//

    player_interaction[] interactions;
    public override player_interaction[] item_uses()
    {
        if (interactions == null)
            interactions = new player_interaction[]
            {
                new build_interaction(this),
                new demolish_interaction(this)
            };
        return interactions;
    }

    public bool is_blueprint { get; private set; }

    snap_point closest_to_ray(Ray ray, float ray_distance)
    {
        snap_point ret = null;

        // Attempt to raycast to this item/find the nearest
        // snap_point to the raycast hit
        RaycastHit hit;
        if (utils.raycast_for_closest<item>(
            ray, out hit, ray_distance,
            (h, t) => t == this))
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

    public override void on_equip(player player)
    {
        base.on_equip(player);

        if (player.has_authority)
        {
            // Draw links if a path element is equipped
            if (GetComponentInChildren<town_path_element>() != null)
                town_path_element.draw_links = true;
        }
    }

    public override void on_unequip(player player)
    {
        base.on_unequip(player);

        if (player.has_authority)
        {
            // Stop drawing debug-type things
            town_path_element.draw_links = false;
            item_node.display_enabled = false;
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

    building_material blueprint_and_fix_to(
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

    building_material blueprint_and_fix_at(RaycastHit hit)
    {
        var spawned = (building_material)create(name, hit.point, Quaternion.identity);
        spawned.make_blueprint();

        spawned.weld = new weld_info(spawned,
            hit.point,
            player.current.transform.rotation);

        return spawned;
    }

    class demolish_interaction : player_interaction
    {
        public static float last_time_deleting { get; private set; }

        building_material equipped;
        public demolish_interaction(building_material equipped) { this.equipped = equipped; }

        public override controls.BIND keybind => controls.BIND.ALT_USE_ITEM;
        public override bool allow_held => true;

        public override string context_tip()
        {
            return "destroy matching objects";
        }

        public override bool start_interaction(player player)
        {
            return base.start_interaction(player);
        }

        public override bool continue_interaction(player player)
        {
            // Stop cancelling a build with right-click from immediately 
            // destroying the object we were welding to during the build.
            const float TIME_BETWEEN_DELETES = 0.1f;
            if (Time.realtimeSinceStartup < last_time_deleting + TIME_BETWEEN_DELETES ||
                Time.realtimeSinceStartup < build_interaction.last_time_building + TIME_BETWEEN_DELETES)
                return true;

            // Right click destroys items of the same kind
            var camera_ray = player.camera_ray(BUILD_RANGE, out float dis);
            building_material found_same = utils.raycast_for_closest<building_material>(
                camera_ray, out RaycastHit hit, dis, (h, b) => b.name == equipped.name);
            if (found_same == null) return true;

            last_time_deleting = Time.realtimeSinceStartup;
            found_same.pick_up(register_undo: true);
            player.current.play_sound("sounds/hammer_wood_lowpass", 0.9f, 1.1f, 0.5f, location: hit.point);
            return true;
        }
    }

    class build_interaction : player_interaction
    {
        public static float last_time_building { get; private set; }

        building_material blueprint;
        building_material equipped;

        public build_interaction(building_material equipped) { this.equipped = equipped; }
        public override controls.BIND keybind => controls.BIND.USE_ITEM;

        public override string context_tip()
        {
            if (blueprint != null)
            {
                if (controls.key_based_building)
                {
                    return "build, " + controls.bind_name(controls.BIND.ALT_USE_ITEM) + "to cancel \nUse " +
                        controls.bind_name(controls.BIND.MANIPULATE_BUILDING_FORWARD) + ", " +
                        controls.bind_name(controls.BIND.MANIPULATE_BUILDING_LEFT) + ", " +
                        controls.bind_name(controls.BIND.MANIPULATE_BUILDING_BACK) + ", " +
                        controls.bind_name(controls.BIND.MANIPULATE_BUILDING_RIGHT) + ", " +
                        controls.bind_name(controls.BIND.MANIPULATE_BUILDING_DOWN) + " and " +
                        controls.bind_name(controls.BIND.MANIPULATE_BUILDING_UP) + " to rotate the building\n" +
                        "Hold " + controls.bind_name(controls.BIND.BUILDING_TRANSLATION) + " to translate instead\n" +
                        "Scroll, or press " + controls.bind_name(controls.BIND.INCREMENT_PIVOT) +
                        " to cycle initial orientations (purple spheres)\n" +
                        "Hold " + controls.bind_name(controls.BIND.FINE_ROTATION) + " to disable rotation snapping";
                }
                else
                {
                    return
                        "Double tap " + controls.bind_name(keybind) + " to build, " + controls.bind_name(controls.BIND.ALT_USE_ITEM) + " to cancel\n" +
                        "Click and drag the arrows to translate, or the circles to rotate\n" +
                        "Scroll, or press " + controls.bind_name(controls.BIND.INCREMENT_PIVOT) + " to cycle initial orientations (purple spheres)\n" +
                        "Hold " + controls.bind_name(controls.BIND.FINE_ROTATION) + " to disable rotation snapping";
                }
            }

            return "build\n" +
                "Buildings will snap together at key points, hold " +
                controls.bind_name(controls.BIND.IGNORE_SNAP_POINTS) + " to disable this\n" +
                "Disabling snapping will also allign the building to the world axes";
        }

        public override bool start_interaction(player player)
        {
            // Don't do anything on the non-authority client
            if (!player.has_authority) return true;

            if (equipped.snap_points.Length == 0)
                throw new System.Exception("No snap points found on " + equipped.display_name + "!");

            // Get the ray to cast along, that stays within 
            // BUILD_RANGE of the player
            float raycast_distance = 0;
            var camera_ray = player.current.camera_ray(BUILD_RANGE, out raycast_distance);

            // Find a (non-logistics version) building_material/snap_point 
            // under cursor (unless ignore_snap_points is held)
            RaycastHit hit = default;
            building_material bm = null;
            if (!controls.held(controls.BIND.IGNORE_SNAP_POINTS))
                bm = utils.raycast_for_closest<building_material>(
                    camera_ray, out hit, raycast_distance, accept: (h, b) => !b.is_logistics_version);

            // If a building material is found, fix new build to it
            // otherwise, just fix to any solid object
            if (bm != null)
                blueprint = equipped.blueprint_and_fix_to(bm, hit, camera_ray, raycast_distance);
            else
            {
                var col = utils.raycast_for_closest<Collider>(camera_ray, out hit, raycast_distance,
                    (h, c) => !c.transform.IsChildOf(player.current.transform));

                if (col != null) blueprint = equipped.blueprint_and_fix_at(hit);
            }

            // Move onto rotation stage if something was spawned
            if (blueprint != null)
            {
                player.current.play_sound("sounds/hammer_wood_lowpass", 0.9f, 1.1f, 0.5f, location: hit.point);
                if (!controls.key_based_building) player.current.cursor_sprite = cursors.DEFAULT;
                return false;
            }
            return true;
        }

        public override bool continue_interaction(player player)
        {
            // Don't do anything on the non-authority client
            if (!player.has_authority) return true;
            if (blueprint == null) return true;
            last_time_building = Time.realtimeSinceStartup;

            if (controls.triggered(controls.BIND.ALT_USE_ITEM))
            {
                // Cancel build on right click
                blueprint.weld.on_finish();
                Destroy(blueprint.gameObject);
                blueprint = null;
                return true;
            }

            if (controls.key_based_building)
            {
                if (controls.triggered(controls.BIND.USE_ITEM)) return true;
                blueprint.weld.key_rotate();
                return false;
            }
            else return blueprint.weld.mouse_rotate();
        }

        public override void end_interaction(player player)
        {
            base.end_interaction(player);

            // Don't do anything on the authority client
            if (!player.has_authority) return;

            if (blueprint != null)
            {
                // Remove the object we're building from the inventory
                if (player.current.inventory.remove(blueprint, 1))
                {
                    player.current.play_sound("sounds/hammer_wood", 0.9f, 1.1f, 0.5f, location: blueprint.weld.weld_location);

                    // Create a proper, networked version of the spawned object
                    var created = (building_material)create(blueprint.name,
                        blueprint.transform.position, blueprint.transform.rotation,
                        networked: true, register_undo: true);

                    created.on_build();
                    blueprint.weld.on_finish();
                    Destroy(blueprint.gameObject);

                    // Satisfy building requirements
                    build_requirement.on_build(created);

                }
            }

            blueprint = null;
            player.current.validate_equip();
        }
    }

    /// <summary> Called when the object is built. </summary>
    protected virtual void on_build() { }
}

public abstract class building_with_inventory : building_material
{
    public inventory inventory { get; private set; }

    protected override bool can_pick_up(out string message)
    {
        if (inventory != null && !inventory.empty)
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