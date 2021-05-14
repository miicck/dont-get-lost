using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Component attached to a building to make it into a blueprint
public class blueprint : MonoBehaviour
{
    public building_material building { get; private set; }
    axes axes;
    axes rotation_axes;
    Vector3 mouse_drag_offset;
    float last_click_time;
    float accumulated_adjustment;
    float last_adjustment_played;
    float accumulated_rotation;

    int pivot_index
    {
        get
        {
            // Get pivot index from dictionary based on building name (so it
            // persists between builds) defaults to 0
            if (pivot_indicies.TryGetValue(building.name, out int ind)) return ind;
            pivot_indicies[building.name] = 0;
            return 0;
        }
        set
        {
            // Select the snap point based on the given index
            var sps = building.GetComponentsInChildren<snap_point>(includeInactive: true);
            int ind = utils.positive_mod(value, sps.Length);
            var sp = sps[ind];

            // Update the dictionary
            pivot_indicies[building.name] = ind;

            // Rotate building so that the snap point is alligned
            Quaternion snap_point_rot = Quaternion.Inverse(building.transform.rotation) * sp.transform.rotation;
            building.transform.rotation = transform.rotation;
            building.transform.rotation *= Quaternion.Inverse(snap_point_rot);

            // Translate the building so that the snap point is alligned
            Vector3 delta = building.transform.position - sp.transform.position;
            building.transform.position = transform.position + delta;
        }
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

    MOUSE_MODE mouse_mode
    {
        get => _mouse_mode;
        set
        {
            _mouse_mode = value;
            switch (_mouse_mode)
            {
                // Highlight the axes corresponding to the given mouse mode
                case MOUSE_MODE.X_TRANSLATE: axes.highlight_axis(axes.AXIS.X); break;
                case MOUSE_MODE.Y_TRANSLATE: axes.highlight_axis(axes.AXIS.Y); break;
                case MOUSE_MODE.Z_TRANSLATE: axes.highlight_axis(axes.AXIS.Z); break;
                case MOUSE_MODE.X_ROTATE: rotation_axes.highlight_axis(axes.AXIS.X); break;
                case MOUSE_MODE.Y_ROTATE: rotation_axes.highlight_axis(axes.AXIS.Y); break;
                case MOUSE_MODE.Z_ROTATE: rotation_axes.highlight_axis(axes.AXIS.Z); break;
                case MOUSE_MODE.NONE:
                    axes.highlight_axis(axes.AXIS.NONE);
                    rotation_axes.highlight_axis(axes.AXIS.NONE);
                    break;
            }
        }
    }
    MOUSE_MODE _mouse_mode = MOUSE_MODE.NONE;

    void listen_for_pivot_change()
    {
        float pivot_change_dir = controls.delta(controls.BIND.CHANGE_PIVOT);
        if (controls.triggered(controls.BIND.INCREMENT_PIVOT)) pivot_change_dir = 1f;
        if (pivot_change_dir != 0)
        {
            // Change the pivot
            pivot_index += pivot_change_dir > 0 ? 1 : -1;
            player.current.play_sound("sounds/adjustment_click", 1f, 1f, 0.5f,
                location: transform.position, min_time_since_last: 0.05f);
        }
    }

    void play_sounds()
    {
        // Play adjustment sounds
        if (accumulated_adjustment > last_adjustment_played + 1)
        {
            last_adjustment_played = accumulated_adjustment;

            float x = Mathf.Max(accumulated_adjustment - 1f, 0f);
            float pitch = 1.5f - 0.5f * Mathf.Exp(-x / 10f);

            player.current.play_sound("sounds/adjustment_click", pitch, pitch, 0.5f,
                location: transform.position, min_time_since_last: 0.05f);
        }
    }

    public bool manipulate()
    {
        listen_for_pivot_change();
        play_sounds();

        if (controls.key_based_building)
        {
            if (controls.triggered(controls.BIND.USE_ITEM)) return true;
            manipulate_with_keys();
            return false;
        }

        return manipulate_with_mouse();
    }

    bool manipulate_with_mouse()
    {
        // The ray from the player camera
        var cam_ray = player.current.camera_ray();

        if (controls.triggered(controls.BIND.USE_ITEM))
        {
            // Check for double-trigger to place
            if (Time.time - last_click_time < 0.5f) return true;
            last_click_time = Time.time;

            // See if we've triggered one of the axes
            var trans = utils.raycast_for_closest<Transform>(cam_ray, out RaycastHit hit,
                 accept: (h, t) => t.IsChildOf(axes.transform) || t.IsChildOf(rotation_axes.transform));

            // Test for selection of translation mode
            switch (axes.test_is_part_of_axis(trans))
            {
                case axes.AXIS.X: mouse_mode = MOUSE_MODE.X_TRANSLATE; break;
                case axes.AXIS.Y: mouse_mode = MOUSE_MODE.Y_TRANSLATE; break;
                case axes.AXIS.Z: mouse_mode = MOUSE_MODE.Z_TRANSLATE; break;
            }

            // Test for selection of rotation mode
            switch (rotation_axes.test_is_part_of_axis(trans))
            {
                case axes.AXIS.X: mouse_mode = MOUSE_MODE.X_ROTATE; break;
                case axes.AXIS.Y: mouse_mode = MOUSE_MODE.Y_ROTATE; break;
                case axes.AXIS.Z: mouse_mode = MOUSE_MODE.Z_ROTATE; break;
            }

            // Set the offset from transform.position where the mouse clicked
            switch (mouse_mode)
            {
                case MOUSE_MODE.X_TRANSLATE:
                    mouse_drag_offset = utils.nearest_point_on_line(transform.right_ray(), cam_ray);
                    break;

                case MOUSE_MODE.Y_TRANSLATE:
                    mouse_drag_offset = utils.nearest_point_on_line(transform.up_ray(), cam_ray);
                    break;

                case MOUSE_MODE.Z_TRANSLATE:
                    mouse_drag_offset = utils.nearest_point_on_line(transform.forward_ray(), cam_ray);
                    break;

                default:
                    mouse_drag_offset = transform.position;
                    break;
            }
            mouse_drag_offset -= transform.position;

        }
        else if (controls.untriggered(controls.BIND.USE_ITEM))
        {
            // Un-triggered => reset everything
            mouse_mode = MOUSE_MODE.NONE;
            accumulated_adjustment = 0;
            last_adjustment_played = 0;
        }

        switch (mouse_mode)
        {
            case MOUSE_MODE.X_TRANSLATE:
                translate_to(utils.nearest_point_on_line(transform.right_ray(), cam_ray) - mouse_drag_offset);
                break;

            case MOUSE_MODE.Y_TRANSLATE:
                translate_to(utils.nearest_point_on_line(transform.up_ray(), cam_ray) - mouse_drag_offset);
                break;

            case MOUSE_MODE.Z_TRANSLATE:
                translate_to(utils.nearest_point_on_line(transform.forward_ray(), cam_ray) - mouse_drag_offset);
                break;

            case MOUSE_MODE.X_ROTATE:
                rotate_around(transform.right);
                break;

            case MOUSE_MODE.Y_ROTATE:
                rotate_around(transform.up);
                break;

            case MOUSE_MODE.Z_ROTATE:
                rotate_around(-transform.forward);
                break;
        }

        return false;
    }

    void manipulate_with_keys()
    {
        if (controls.held(controls.BIND.BUILDING_TRANSLATION))
        {
            axes.gameObject.SetActive(true);
            rotation_axes.gameObject.SetActive(false);
            translate_with_keys();
        }
        else
        {
            axes.gameObject.SetActive(false);
            rotation_axes.gameObject.SetActive(true);
            rotate_with_keys();
        }
    }

    void translate_with_keys()
    {
        if (controls.held(controls.BIND.MANIPULATE_BUILDING_RIGHT))
        {
            axes.highlight_axis(axes.AXIS.X);
            translate_to(transform.position + transform.right * Time.deltaTime / 2f);
        }
        else if (controls.held(controls.BIND.MANIPULATE_BUILDING_LEFT))
        {
            axes.highlight_axis(axes.AXIS.X);
            translate_to(transform.position - transform.right * Time.deltaTime / 2f);
        }
        else if (controls.held(controls.BIND.MANIPULATE_BUILDING_FORWARD))
        {
            axes.highlight_axis(axes.AXIS.Z);
            translate_to(transform.position + transform.forward * Time.deltaTime / 2f);
        }
        else if (controls.held(controls.BIND.MANIPULATE_BUILDING_BACK))
        {
            axes.highlight_axis(axes.AXIS.Z);
            translate_to(transform.position - transform.forward * Time.deltaTime / 2f);
        }
        else if (controls.held(controls.BIND.MANIPULATE_BUILDING_UP))
        {
            axes.highlight_axis(axes.AXIS.Y);
            translate_to(transform.position + transform.up * Time.deltaTime / 2f);
        }
        else if (controls.held(controls.BIND.MANIPULATE_BUILDING_DOWN))
        {
            axes.highlight_axis(axes.AXIS.Y);
            translate_to(transform.position - transform.up * Time.deltaTime / 2f);
        }
    }

    delegate bool trigger_func(controls.BIND b);

    void rotate_with_keys()
    {
        trigger_func trigger = (b) => controls.triggered(b);
        float amount = 45f;

        if (controls.held(controls.BIND.FINE_ROTATION))
        {
            trigger = (b) => controls.held(b);
            amount = Time.deltaTime * 15f;
        }

        if (trigger(controls.BIND.MANIPULATE_BUILDING_RIGHT))
        {
            rotate_around(transform.forward, -amount);
            rotation_axes.highlight_axis(axes.AXIS.Z);
        }
        else if (trigger(controls.BIND.MANIPULATE_BUILDING_LEFT))
        {
            rotate_around(transform.forward, amount);
            rotation_axes.highlight_axis(axes.AXIS.Z);
        }
        else if (trigger(controls.BIND.MANIPULATE_BUILDING_BACK))
        {
            rotate_around(transform.right, -amount);
            rotation_axes.highlight_axis(axes.AXIS.X);
        }
        else if (trigger(controls.BIND.MANIPULATE_BUILDING_FORWARD))
        {
            rotate_around(transform.right, amount);
            rotation_axes.highlight_axis(axes.AXIS.X);
        }
        else if (trigger(controls.BIND.MANIPULATE_BUILDING_DOWN))
        {
            rotate_around(transform.up, -amount);
            rotation_axes.highlight_axis(axes.AXIS.Y);
        }
        else if (trigger(controls.BIND.MANIPULATE_BUILDING_UP))
        {
            rotate_around(transform.up, amount);
            rotation_axes.highlight_axis(axes.AXIS.Y);
        }
    }

    void translate_to(Vector3 v)
    {
        Vector3 delta = v - transform.position;
        accumulated_adjustment += delta.magnitude / 0.1f;
        transform.position = v;
    }

    void rotate_around(Vector3 axis, float angle)
    {
        accumulated_adjustment += Mathf.Abs(angle) / 15f;
        Transform to_rotate = controls.held(controls.BIND.ROTATE_ALSO_AXES) ? transform : building.transform;
        to_rotate.RotateAround(transform.position, axis, angle);
    }

    void rotate_around(Vector3 axis)
    {
        float amount = controls.delta(controls.BIND.ROTATION_AMOUNT_X);
        amount += controls.delta(controls.BIND.ROTATION_AMOUNT_Y);
        amount *= 10f;

        if (!controls.held(controls.BIND.FINE_ROTATION))
        {
            // Snap rotation to 45 degree increments
            accumulated_rotation += amount;
            if (accumulated_rotation > 45f)
            {
                accumulated_rotation = 0f;
                amount = 45f;
            }
            else if (accumulated_rotation < -45f)
            {
                accumulated_rotation = 0f;
                amount = -45f;
            }
            else return;
        }

        rotate_around(axis, amount);
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    private void Start()
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

        // Create the translation axes
        axes = Resources.Load<axes>("misc/axes").inst();
        axes.transform.SetParent(transform);
        axes.transform.localPosition = Vector3.zero;
        axes.transform.localRotation = Quaternion.identity;

        // Create the rotation axes
        rotation_axes = Resources.Load<axes>("misc/rotation_axes").inst();
        rotation_axes.transform.SetParent(transform);
        rotation_axes.transform.localPosition = Vector3.zero;
        rotation_axes.transform.localRotation = Quaternion.identity;

        // Initialize the pivot
        pivot_index = pivot_index;
        last_click_time = Time.time;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static Dictionary<string, int> pivot_indicies = new Dictionary<string, int>();

    public static blueprint create(string building_name, Vector3 position, Quaternion rotation)
    {
        // No building with the given name
        if (Resources.Load<building_material>("items/" + building_name) == null)
            return null;

        // Create the bluerprint
        var bp = new GameObject(building_name + "(blueprint)").AddComponent<blueprint>();
        bp.transform.position = position;
        bp.transform.rotation = rotation;

        // Create the building/make a child of the blueprint
        var bm = item.create(building_name, position, rotation) as building_material;
        bm.enabled = false; // Disable the building_material component
        bm.transform.SetParent(bp.transform);
        bp.building = bm;

        return bp;
    }
}