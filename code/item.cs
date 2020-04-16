using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item : interactable
{
    //###########//
    // CONSTANTS //
    //###########//

    const float CARRY_RESTORE_FORCE = 10f;
    public const float WELD_RANGE = 5f;

    //###########//
    // VARIABLES //
    //###########//

    public Sprite sprite;

    new public string name { get { return parse_id_name(base.name).Value; } }
    public int id { get { return parse_id_name(base.name).Key; } }

    //#########//
    // WELDING //
    //#########//

    // Points that welds snap to
    snap_point[] snap_points { get { return GetComponentsInChildren<snap_point>(); } }

    // A weld represents the fixture of an item via a pivot
    // to a particular weld point in space
    public class weld_info
    {
        item to_weld;                       // The item being welded
        snap_point pivot;                   // The pivot of the item, in snapped orientation
        Vector3 weld_location;              // The world location of the weld
        Quaternion weld_rotation;           // The rotation of the weld (specifying the snap directions)

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
        void key_rotate()
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

        public void rotate(float x, float y)
        {
            key_rotate();

            // The mouse movement in the plane of the camera view
            Vector3 mouse_dir = player.current.camera.transform.right * x +
                                player.current.camera.transform.up * y;

            canvas.set_direction_indicator(new Vector2(x, y));

            if (mouse_dir.magnitude < 5) return;

            // Find the axis most alligned with the mouse movement 
            // (ignoring component in forward direction)
            Vector3 up_axis = utils.find_to_min(snap_axes(), (a) =>
            {
                a -= Vector3.Project(a, player.current.camera.transform.forward);
                return Vector3.Dot(mouse_dir.normalized, a.normalized);
            });

            set_pivot_rotation(Quaternion.LookRotation(pivot.transform.forward, up_axis));
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

    void fix_to(item other)
    {
        snap_point snap_from = this.closest_to_ray(player.current.camera_ray());
        snap_point snap_to = other.closest_to_ray(player.current.camera_ray());

        if (snap_from == null) return;
        if (snap_to == null) return;

        weld = new weld_info(this,
            snap_from,
            snap_to.transform.position,
            Quaternion.identity);
    }

    void fix_at(RaycastHit hit)
    {
        snap_point snap_from = this.closest_to_ray(player.current.camera_ray());

        if (snap_from == null) return;

        weld = new weld_info(this,
            snap_from,
            hit.point,
            Quaternion.identity);
    }

    //#####################//
    // PLAYER INTERACTIONS //
    //#####################//

    new public Rigidbody rigidbody { get; private set; }
    Transform carry_pivot;
    float carry_distance;

    public override FLAGS player_interact()
    {
        // Drop item
        if (weld != null && Input.GetMouseButtonDown(1) ||
            Input.GetMouseButtonDown(0))
        {
            stop_interaction();
            return FLAGS.NONE;
        }

        if (weld == null) // Item is not welded
        {
            // Snap item to carrying point
            Vector3 carry_point = player.current.camera.transform.position +
                 carry_distance * player.current.camera.transform.forward;
            transform.position += carry_point - carry_pivot.position;

            // Move item towards/away on scroll
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0) carry_distance *= 1.2f;
            else if (scroll < 0) carry_distance /= 1.2f;
            if (carry_distance > WELD_RANGE) carry_distance = WELD_RANGE;
        }
        else // Item is welded
        {
            // Unweld on right click
            if (Input.GetMouseButtonDown(1))
                weld = null;
            else
            {
                weld.rotate(5 * Input.GetAxis("Mouse X"),
                            5 * Input.GetAxis("Mouse Y"));
                return FLAGS.DISALLOWS_ROTATION | FLAGS.DISALLOWS_MOVEMENT;
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            // Find an item to snap to
            RaycastHit hit;
            item other = utils.raycast_for_closest<item>(
                player.current.camera_ray(), out hit,
                WELD_RANGE, (t) => t != this);

            if (other != null)
                fix_to(other);
            else
            {
                // Raycast for a surface to weld to
                Component closest = utils.raycast_for_closest<Component>(
                    player.current.camera_ray(), out hit,
                    WELD_RANGE, (c) => !c.transform.IsChildOf(transform));

                if (closest != null)
                    fix_at(hit);
            }
        }

        return FLAGS.NONE;
    }

    public override void on_start_interaction(
        RaycastHit point_hit, item interact_with, INTERACT_TYPE type)
    {
        switch (type)
        {
            // Carry the item on right click
            case INTERACT_TYPE.RIGHT_CLICK:

                // Create the pivot point that we clicked the item at
                carry_pivot = new GameObject("pivot").transform;
                carry_pivot.SetParent(transform);
                carry_pivot.transform.position = point_hit.point;
                carry_pivot.rotation = player.current.camera.transform.rotation;

                // Item is under player ownership
                transform.SetParent(player.current.camera.transform);

                // Setup item in carry mode
                weld = null;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = false;
                carry_distance = 2f;
                break;

            // Default to just picking up the item
            default:
                if (player.current.inventory.add(name, 1))
                {
                    popup_message.create("+ 1 " + name);
                    Destroy(this.gameObject);
                }
                stop_interaction();
                return;
        }
    }

    public override void on_end_interaction()
    {
        // Return the item to chunk-ownership  
        transform.SetParent(chunk.at(transform.position).transform);

        // Setup item in world mode
        if (weld == null) rigidbody.isKinematic = false;
        rigidbody.detectCollisions = true;
        if (carry_pivot != null) Destroy(carry_pivot.gameObject);
        canvas.set_direction_indicator(Vector2.zero);
    }

    public override string cursor()
    {
        return cursors.GRAB_CLOSED;
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    private void OnDrawGizmos()
    {
        if (weld != null) weld.draw_gizmos();
    }

    private void Update()
    {
        // Fallen through the map, get rid of
        if (transform.position.y < -10f)
        {
            rigidbody.velocity = Vector3.zero;
            transform.position = player.current.camera.transform.position;
        }
    }

    //###############//
    // SERIALIZATION //
    //###############//

    public byte[] serialize()
    {
        const int IS = sizeof(int);
        const int FS = sizeof(float);

        // Floating point values to serialize
        Vector3 euler = transform.rotation.eulerAngles;
        float[] floats = new float[]
        {
            transform.position.x,
            transform.position.y,
            transform.position.z,
            transform.rotation.x,
            transform.rotation.y,
            transform.rotation.z,
            transform.rotation.w
        };

        // Integers to serialize
        int[] ints = new int[]
        {
            id,
            rigidbody.isKinematic ? 1 : 0,
        };

        // Serialize into byte array
        int length = floats.Length * FS + ints.Length * IS;
        var bytes = new byte[length];

        for (int i = 0; i < ints.Length; ++i)
        {
            var ib = System.BitConverter.GetBytes(ints[i]);
            System.Buffer.BlockCopy(ib, 0, bytes, i * IS, IS);
        }

        for (int i = 0; i < floats.Length; ++i)
        {
            var fb = System.BitConverter.GetBytes(floats[i]);
            System.Buffer.BlockCopy(fb, 0, bytes, ints.Length * IS + i * FS, FS);
        }

        return bytes;
    }

    public static item deserailize(byte[] bytes)
    {
        const int IS = sizeof(int);
        const int FS = sizeof(float);

        // Deserailize id
        int id = System.BitConverter.ToInt32(bytes, 0);
        bool is_kinematic = System.BitConverter.ToInt32(bytes, IS) != 0;

        // Deserailzie position
        Vector3 position;
        position.x = System.BitConverter.ToSingle(bytes, IS * 2);
        position.y = System.BitConverter.ToSingle(bytes, IS * 2 + FS);
        position.z = System.BitConverter.ToSingle(bytes, IS * 2 + FS * 2);

        // Desearialize rotation
        Quaternion rot = new Quaternion(
            System.BitConverter.ToSingle(bytes, IS * 2 + FS * 3),
            System.BitConverter.ToSingle(bytes, IS * 2 + FS * 4),
            System.BitConverter.ToSingle(bytes, IS * 2 + FS * 5),
            System.BitConverter.ToSingle(bytes, IS * 2 + FS * 6)
            );

        // Create the item
        var i = create(id, position, rot);
        i.rigidbody.isKinematic = is_kinematic;
        return i;
    }

    //##############//
    // CREATION ETC //
    //##############//

    public item spawn_copy(Vector3 position)
    {
        return spawn(name, position);
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static KeyValuePair<int, string> parse_id_name(string to_parse)
    {
        int us_index = to_parse.IndexOf("_");
        int id = int.Parse(to_parse.Substring(0, us_index));
        string name = to_parse.Substring(us_index + 1);
        return new KeyValuePair<int, string>(id, name);
    }

    public static item spawn(string name, Vector3 position)
    {
        var rot = Quaternion.Euler(0, Random.Range(0, 360f), 0);
        var i = create(id_from_name(name), position, rot);
        i.rigidbody.velocity = Random.onUnitSphere;
        return i;
    }

    static item create(int id, Vector3 position, Quaternion rotation)
    {
        var i = load_from_id(id).inst();
        i.transform.position = position;
        i.transform.rotation = rotation;
        i.rigidbody = i.gameObject.AddComponent<Rigidbody>();
        i.transform.SetParent(chunk.at(i.transform.position).transform);
        return i;
    }

    // Lookup tables for items by name or id
    static Dictionary<int, item> item_by_id;
    static Dictionary<string, int> id_by_name;

    // Load the above lookup tables
    static void load_item_libraries()
    {
        item_by_id = new Dictionary<int, item>();
        id_by_name = new Dictionary<string, int>();

        foreach (var i in Resources.LoadAll<item>("items/"))
        {
            item_by_id[i.id] = i;
            id_by_name[i.name] = i.id;
        }
    }

    // Load an item by name
    public static item load_from_name(string name)
    {
        return load_from_id(id_from_name(name));
    }

    // Load an item by id
    public static item load_from_id(int id)
    {
        if (item_by_id == null)
            load_item_libraries();
        return item_by_id[id];
    }

    // Get an item id from an item name
    public static int id_from_name(string name)
    {
        if (id_by_name == null)
            load_item_libraries();
        return id_by_name[name];
    }
}