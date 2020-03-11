using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class inventory
{
    Dictionary<string, int> contents
        = new Dictionary<string, int>();

    public bool add_item(string item_name)
    {
        if (contents.ContainsKey(item_name)) contents[item_name] += 1;
        else contents[item_name] = 1;
        return true;
    }

    public string get_info()
    {
        string info = "Inventory:";
        foreach (var kv in contents)
            info += "\n    " + kv.Value + " " + kv.Key;
        return info;
    }

    public int get_count(string item)
    {
        int count = 0;
        if (contents.TryGetValue(item, out count)) { }
        return count;
    }
}

public class player : MonoBehaviour
{
    // Dimensions of a player
    public const float HEIGHT = 1.8f;
    public const float WIDTH = 0.45f;
    public const float EYE_HEIGHT = HEIGHT - WIDTH / 2;
    public const float SPEED = 10f;
    public const float ROTATION_SPEED = 90f;
    public const float JUMP_VEL = 5f;
    public const float GROUND_TEST_DIST = 0.15f;
    public const float TERRAIN_SINK_ALLOW = 0.2f;
    public const float TERRAIN_SINK_RESET_DIST = GROUND_TEST_DIST;
    public const float INTERACTION_RANGE = 3f;
    public const float ITEM_ANCHOR_SEARCH_RANGE = 5f;
    public const float MAP_CAMERA_ALT = world.MAX_ALTITUDE * 2;
    public const float MAP_CAMERA_CLIP = world.MAX_ALTITUDE * 3;
    public const float MAP_SHADOW_DISTANCE = world.MAX_ALTITUDE * 3;
    public const float MAP_OBSCURER_ALT = world.MAX_ALTITUDE * 1.5f;
    public const int MAX_MOVE_PROJ_REMOVE = 4;

    public static new Camera camera { get; private set; }
    public static inventory inventory { get; private set; }

    public static Vector3 item_attraction_point
    {
        get { return camera.transform.position + Vector3.down; }
    }

    GameObject obscurer;
    GameObject map_obscurer;

    public void update_render_range()
    {
        obscurer.transform.localScale = Vector3.one * game.render_range;
        map_obscurer.transform.localScale = Vector3.one * game.render_range;
        if (!map_open)
        {
            camera.farClipPlane = game.render_range;
            QualitySettings.shadowDistance = camera.farClipPlane;
        }
    }

    // The position of the upper sphere of the player
    // (used for capsule-based collision)
    public Vector3 upperSpherePosition
    {
        get { return transform.position + Vector3.up * (HEIGHT - WIDTH / 2); }
    }

    // The position of the lower sphere of the player
    // (used for capsule-based collision)
    public Vector3 lowerSpherePosition
    {
        get { return transform.position + Vector3.up * WIDTH / 2; }
    }

    // Returns true if the player is on the ground
    bool grounded
    {
        get
        {
            return Physics.CapsuleCast(
                lowerSpherePosition, upperSpherePosition,
                WIDTH / 2, Vector3.down, GROUND_TEST_DIST);
        }
    }

    float yvel = 0;

    void OnDrawGizmos()
    {
        if (grounded) Gizmos.color = Color.green;
        else Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(upperSpherePosition, WIDTH / 2);
        Gizmos.DrawWireSphere(lowerSpherePosition, WIDTH / 2);
    }

    void tryMove(Vector3 move, int attempts = 1)
    {
        // Max attempts = The number of projections that 
        // cause collisions which can be removed from "move"
        // before the whole move is rejected.
        if (attempts > MAX_MOVE_PROJ_REMOVE) return;

        // Check if this move will cause a collision
        RaycastHit hit;
        if (Physics.CapsuleCast(
            lowerSpherePosition, upperSpherePosition,
            WIDTH / 2, move.normalized, out hit, move.magnitude))
        {
            // Remove the offending projection of 
            // the proposed move and try again
            move -= 1.01f * Vector3.Project(move, hit.normal);
            tryMove(move, attempts + 1);
            return;
        }

        // Make the move
        transform.position += move;
    }

    // Force the player to stay slightly above the terrain
    void stay_above_terrain()
    {
        if (transform.position.y < 0)
        {
            Vector3 pos = transform.position;
            pos.y = 0;
            transform.position = pos;
        }

        var hits = Physics.RaycastAll(
            transform.position + Vector3.up * world.MAX_ALTITUDE,
            Vector3.down, world.MAX_ALTITUDE + TERRAIN_SINK_ALLOW);
        foreach (var hit in hits)
            if (hit.collider != null)
            {
                var terr = hit.collider.gameObject.GetComponent<Terrain>();
                if (terr != null)
                {
                    Vector3 pos = transform.position;
                    pos.y = hit.point.y + TERRAIN_SINK_RESET_DIST;
                    transform.position = pos;
                }
            }
    }

    void Update()
    {
        var manipulation_performed = ITEM_MANIPULATION.NONE;
        if (item_carrying != null)
        {
            // We have an item
            // Set the cursor to the carrying symbol
            canvas.cursor = cursors.GRAB_CLOSED;

            // Manipulate the item, recording the manipulation performed
            manipulation_performed = manipulate_item();

            // Drop the item if not left clicking
            if (!Input.GetMouseButton(0))
                item_carrying = null;
        }

        // Not carrying an item, try to pick one up
        else
        {
            // Try to pick up an item
            attempt_pickup_item();

            // If nothing picked up, we can harvest stuff
            if (item_carrying == null)
                harvest_stuff();
        }

        // Carry out movement logic if we are not rotating a weld
        if (manipulation_performed != ITEM_MANIPULATION.ROTATING_WELD)
            move();
    }

    void move()
    {
        // Toggle the map view
        if (Input.GetKeyDown(KeyCode.M))
            map_open = !map_open;

        // Don't go below the terrain
        stay_above_terrain();

        if (map_open)
        {
            // Zoom the map
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0) game.render_range_target /= 1.2f;
            else if (scroll < 0) game.render_range_target *= 1.2f;
            camera.orthographicSize = game.render_range;
        }
        else
        {
            // Rotate the view using the mouse
            // Note that horizontal moves rotate the player
            // vertical moves rotate the camera
            transform.Rotate(0, Input.GetAxis("Mouse X") * 5, 0);
            camera.transform.Rotate(-Input.GetAxis("Mouse Y") * 5, 0, 0);
        }

        // Gravity/Jumping
        if (!grounded) yvel -= 9.81f * Time.deltaTime;
        else if (yvel < 0) yvel = 0;
        else if (Input.GetKeyDown(KeyCode.Space)) yvel = JUMP_VEL;

        // Move in the x-z plane using WASD
        float lr = 0;
        float fb = 0;

        if (Input.GetKey(KeyCode.W)) fb += 1.0f;
        if (Input.GetKey(KeyCode.S)) fb -= 1.0f;
        if (Input.GetKey(KeyCode.A)) lr -= 1.0f;
        if (Input.GetKey(KeyCode.D)) lr += 1.0f;

        Vector3 move = transform.forward * fb;
        float speed = SPEED;

        // Go really fast on shift
        if (Input.GetKey(KeyCode.LeftShift))
        {
            if (!map_open) // Fly in 3d view
                move = camera.transform.forward * fb;
            speed *= 10;
            yvel = 0;
        }

        if (map_open) transform.Rotate(0, lr * Time.deltaTime * ROTATION_SPEED, 0);
        else move += transform.right * lr; // Strafing 

        tryMove(move * Time.deltaTime * speed);
        tryMove(yvel * Vector3.up * Time.deltaTime);
    }

    public Ray camera_ray()
    {
        return new Ray(player.camera.transform.position,
                    player.camera.transform.forward);
    }

    float item_hold_distance = 3;
    public Vector3 item_grab_point()
    {
        return camera.transform.position +
            item_hold_distance * camera.transform.forward;
    }

    item _item_carrying;
    public item item_carrying
    {
        get { return _item_carrying; }
        private set
        {
            if (value == _item_carrying)
                return;

            if (_item_carrying != null)
            {
                _item_carrying.rigidbody.useGravity = true;
                _item_carrying.transform.SetParent(null);
            }

            if (value != null)
            {
                value.rigidbody.useGravity = false;
                value.transform.SetParent(camera.transform);
            }

            _item_carrying = value;
        }
    }

    enum ITEM_MANIPULATION
    {
        NONE,
        CARRYING,
        WELDING,
        ROTATING_WELD,
    }

    ITEM_MANIPULATION manipulate_item()
    {
        // Right mouse button down => "weld" mode
        if (Input.GetMouseButton(1))
        {
            // Rotate the weld with left shift
            if (Input.GetKey(KeyCode.LeftShift))
            {
                // Rotate weld => rotate the current anchor
                item_carrying.rotate_anchor(camera.transform.right,
                    Input.GetAxis("Mouse Y"));
                item_carrying.rotate_anchor(camera.transform.up,
                    -Input.GetAxis("Mouse X"));

                // Snap rotation to 45 degree increments
                if (Input.GetKey(KeyCode.S))
                    item_carrying.snap_anchor_rotation();

                return ITEM_MANIPULATION.ROTATING_WELD;
            }

            // Select the weld point
            // Find the nearest surface to weld the item to
            RaycastHit closest_hit = new RaycastHit();
            float min_dis = float.MaxValue;
            foreach (var h in Physics.RaycastAll(camera_ray(), ITEM_ANCHOR_SEARCH_RANGE))
            {
                if (h.transform.IsChildOf(item_carrying.transform))
                    continue;

                float dis = (h.point - camera.transform.position).magnitude;
                if (dis < min_dis)
                {
                    min_dis = dis;
                    closest_hit = h;
                }
            }

            if (min_dis < float.MaxValue)
            {
                // Anchor the item at the given hit point
                item_carrying.anchor_at(
                    closest_hit.point, closest_hit.normal);
            }
            return ITEM_MANIPULATION.WELDING;
        }

        // Right button not down => "carry" mode

        // Attract item towards grab point (dampen oscillations)
        Vector3 dx = item_grab_point() - item_carrying.pivot.position;
        Vector3 v = item_carrying.rigidbody.velocity;
        item_carrying.rigidbody.AddForce(20 * dx - 4 * v);

        // Move grab point backwards or forwards with scrollwheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0) item_hold_distance *= 1.2f;
        else if (scroll < 0) item_hold_distance /= 1.2f;

        return ITEM_MANIPULATION.CARRYING;
    }

    void attempt_pickup_item()
    {
        // Already have an item
        if (item_carrying != null)
            return;

        // Find the nearest interactable item under the cursor
        float min_dis = float.MaxValue;
        item item_under_cursor = null;
        RaycastHit item_hit = new RaycastHit();
        foreach (var h in Physics.RaycastAll(camera_ray(), INTERACTION_RANGE))
        {
            var hi = h.transform.GetComponentInParent<item>();
            if (hi == null) continue;

            float dis = (h.point - camera.transform.position).magnitude;
            if (dis < min_dis)
            {
                min_dis = dis;
                item_under_cursor = hi;
                item_hit = h;
            }
        }

        // No item under cursor
        if (item_under_cursor == null)
        {
            canvas.cursor = cursors.DEFAULT;
            return;
        }

        canvas.cursor = cursors.DEFAULT_INTERACTION;
        if (Input.GetMouseButtonDown(0))
        {
            // Pick up the item
            item_hold_distance = (camera.transform.position - item_hit.point).magnitude;
            item_carrying = item_under_cursor;

            // Set the anchor to where we clicked the object and
            // ensure the object is unwelded
            item_carrying.set_pivot(item_hit.point, item_hit.normal);
            item_carrying.rigidbody.isKinematic = false;
        }
    }

    void harvest_stuff()
    {
        // Not dealing with items, see if a harvestable
        // object is under the cursor
        Vector3 harvest_point;
        var to_harvest = utils.raycast_for<harvestable>(
            camera_ray(), out harvest_point, INTERACTION_RANGE);

        if (to_harvest == null)
            return;

        canvas.cursor = to_harvest.cursor;
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 spawn_point = (harvest_point +
                camera.transform.position) / 2;
            item.spawn(to_harvest.item, spawn_point);
        }
    }

    Quaternion saved_camera_rotation;
    public bool map_open
    {
        get { return camera.orthographic; }
        set
        {
            if (value)
            {
                saved_camera_rotation = camera.transform.localRotation;

                obscurer.SetActive(false);
                map_obscurer.SetActive(true);

                Vector3 obsc_pos = transform.position;
                obsc_pos.y = MAP_OBSCURER_ALT;
                map_obscurer.transform.position = obsc_pos;

                camera.orthographic = true;
                camera.orthographicSize = game.render_range;
                camera.transform.localPosition = Vector3.up * (MAP_CAMERA_ALT - transform.position.y);
                camera.transform.localRotation = Quaternion.Euler(90, 0, 0);

                camera.farClipPlane = MAP_CAMERA_CLIP;
                QualitySettings.shadowDistance = MAP_SHADOW_DISTANCE;
            }
            else
            {
                obscurer.SetActive(true);
                map_obscurer.SetActive(false);
                camera.orthographic = false;
                camera.transform.SetParent(transform);
                camera.transform.localPosition = Vector3.up * EYE_HEIGHT;
                camera.transform.localRotation = saved_camera_rotation;
            }
        }
    }

    // Create and return a player
    public static player create()
    {
        var player = new GameObject("player").AddComponent<player>();
        player.inventory = new inventory();

        // Create the player camera 
        player.camera = new GameObject("camera").AddComponent<Camera>();
        player.camera.clearFlags = CameraClearFlags.SolidColor;

        // Move the player above the first map chunk so they
        // dont fall off of the map
        player.transform.position = new Vector3(0, world.SEA_LEVEL + 1, 0);

        // Enforce the render limit with a sky-color object
        player.obscurer = Resources.Load<GameObject>("misc/obscurer").inst();
        player.obscurer.transform.SetParent(player.transform);
        player.obscurer.transform.localPosition = Vector3.zero;
        var sky_color = player.obscurer.GetComponentInChildren<Renderer>().material.color;

        player.map_obscurer = Resources.Load<GameObject>("misc/map_obscurer").inst();
        player.map_obscurer.transform.SetParent(player.transform);

        // Make the sky the same color as the obscuring object
        RenderSettings.skybox = null;
        player.camera.backgroundColor = sky_color;

        // Initialize the render range
        player.update_render_range();

        // Start with the map closed
        player.map_open = false;

        return player;
    }
}