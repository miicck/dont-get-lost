using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player : networked_player
{
    //###########//
    // CONSTANTS //
    //###########//

    public const float HEIGHT = 1.5f;
    public const float WIDTH = 0.45f;
    public const float GRAVITY = 10f;
    public const float BOUYANCY = 5f;
    public const float WATER_DRAG = 1.5f;

    public const float SPEED = 7f;
    public const float ACCELERATION_TIME = 0.2f;
    public const float ACCELERATION = SPEED / ACCELERATION_TIME;
    public const float ROTATION_SPEED = 90f;
    public const float JUMP_VEL = 5f;
    public const float THROW_VELOCITY = 6f;

    public const float INTERACTION_RANGE = 3f;

    public const float MAP_CAMERA_ALT = world.MAX_ALTITUDE * 2;
    public const float MAP_CAMERA_CLIP = world.MAX_ALTITUDE * 3;
    public const float MAP_SHADOW_DISTANCE = world.MAX_ALTITUDE * 3;

    //#################//
    // UNITY CALLBACKS //
    //#################//

    void Update()
    {
        if (!local) return;

        // Toggle menus only if not using an item/the map isn't open
        if (current_item_use == USE_TYPE.NOT_USING && !map_open)
        {
            // Toggle inventory on E
            if (Input.GetKeyDown(KeyCode.E))
            {
                inventory_open = !inventory_open;
                crosshairs.enabled = !inventory_open;
                Cursor.visible = inventory_open;
                Cursor.lockState = inventory_open ? CursorLockMode.None : CursorLockMode.Locked;

                // Find a workbench to interact with
                if (inventory_open)
                {
                    RaycastHit hit;
                    var wb = utils.raycast_for_closest<workbench>(camera_ray(), out hit, INTERACTION_RANGE);
                    if (wb != null) current_workbench = wb.inventory;
                }
                else current_workbench = null;
            }
        }

        // Run the quickbar equip shortcuts
        if (current_item_use == USE_TYPE.NOT_USING && !inventory_open && !map_open)
            run_quickbar_shortcuts();

        // Toggle the map view on M
        if (current_item_use == USE_TYPE.NOT_USING)
            if (Input.GetKeyDown(KeyCode.M))
                map_open = !map_open;

        if (map_open)
        {
            // Zoom the map
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0) game.render_range_target /= 1.2f;
            else if (scroll < 0) game.render_range_target *= 1.2f;
            camera.orthographicSize = game.render_range;
        }

        // Use items if the inventory/map aren't open
        item.use_result use_result = item.use_result.complete;
        if (!inventory_open && !map_open)
        {
            if (current_item_use == USE_TYPE.NOT_USING)
            {
                bool left_click = Input.GetMouseButtonDown(0) ||
                    (equipped == null ? false :
                     equipped.allow_left_click_held_down() &&
                     Input.GetMouseButton(0));

                bool right_click = Input.GetMouseButtonDown(1) ||
                    (equipped == null ? false :
                     equipped.allow_right_click_held_down() &&
                     Input.GetMouseButton(1));

                // Start a new use type
                if (left_click)
                {
                    if (equipped == null) left_click_with_hand();
                    else current_item_use = USE_TYPE.USING_LEFT_CLICK;
                }
                else if (right_click)
                {
                    if (equipped == null) right_click_with_hand();
                    else current_item_use = USE_TYPE.USING_RIGHT_CLICK;
                }
            }
            else
            {
                // Continue item use
                if (equipped == null) use_result = item.use_result.complete;
                else use_result = equipped.on_use_continue(current_item_use);
                if (!use_result.underway) current_item_use = USE_TYPE.NOT_USING;
            }

            // Throw equiped on T
            if (use_result.allows_throw)
                if (Input.GetKeyDown(KeyCode.T))
                    if (equipped != null)
                    {
                        inventory.remove(equipped.name, 1);
                        var spawned = item.create(
                            equipped.name,
                            equipped.transform.position, 
                            equipped.transform.rotation);
                        spawned.rigidbody.velocity += camera.transform.forward * THROW_VELOCITY;
                        re_equip();
                    }

            // Look around
            if (use_result.allows_look) mouse_look();
        }

        if (use_result.allows_move)
        {
            move();
            float_in_water();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, game.render_range);
    }

    //###########//
    // INVENTORY //
    //###########//

    const int QUICKBAR_SLOTS_COUNT = 8;

    public inventory inventory { get; private set; }

    inventory _current_workbench;
    inventory current_workbench
    {
        get => _current_workbench;
        set
        {
            if (_current_workbench != null)
                _current_workbench.gameObject.SetActive(false);

            _current_workbench = value;
            if (_current_workbench != null)
            {
                _current_workbench.gameObject.SetActive(true);
                _current_workbench.GetComponent<crafting_input>().craft_to = inventory;
                var rt = _current_workbench.GetComponent<RectTransform>();
                rt.SetParent(inventory.ui_extend_left_point);
                rt.anchoredPosition = Vector2.zero;
            }
        }
    }

    bool inventory_open
    {
        get { return inventory.gameObject.activeInHierarchy; }
        set { inventory.gameObject.SetActive(value); }
    }

    int last_quickbar_slot_accessed = 0;
    public inventory_slot quickbar_slot(int n)
    {
        if (n < 0 || n >= QUICKBAR_SLOTS_COUNT) return null;
        last_quickbar_slot_accessed = n;
        return inventory.slots[n];
    }

    //##########//
    // ITEM USE //
    //##########//

    item _carrying;
    item carrying
    {
        get { return _carrying; }
        set
        {
            if (_carrying != null)
                _carrying.stop_carry();
            _carrying = value;
        }
    }

    // Called on a left click when no item is equipped
    public void left_click_with_hand()
    {
        if (carrying != null) { carrying = null; return; }

        RaycastHit hit;
        item clicked = utils.raycast_for_closest<item>(camera_ray(), out hit, INTERACTION_RANGE);
        if (clicked != null)
            clicked.pick_up();
    }

    // Called on a right click when no item is equipped
    public void right_click_with_hand()
    {
        if (carrying != null) { carrying = null; return; }

        RaycastHit hit;
        item clicked = utils.raycast_for_closest<item>(camera_ray(), out hit, INTERACTION_RANGE);
        if (clicked != null)
        {
            clicked.carry(hit);
            carrying = clicked;
        }
    }

    // The hand which carries an item
    public Transform hand { get; private set; }

    UnityEngine.UI.Image crosshairs;
    public string cursor
    {
        get
        {
            if (crosshairs.sprite == null) return null;
            return crosshairs.sprite.name;
        }
        set
        {
            if (cursor == value) return;
            crosshairs.sprite = Resources.Load<Sprite>("sprites/" + value);
        }
    }

    // The ways that we can use an item
    public enum USE_TYPE
    {
        NOT_USING,
        USING_LEFT_CLICK,
        USING_RIGHT_CLICK,
    }

    // Are we currently using the item, if so, how?
    USE_TYPE _current_item_use;
    USE_TYPE current_item_use
    {
        get { return _current_item_use; }
        set
        {
            if (value == _current_item_use)
                return; // No change

            if (equipped == null)
            {
                // No item to use
                _current_item_use = USE_TYPE.NOT_USING;
                return;
            }

            if (_current_item_use == USE_TYPE.NOT_USING)
            {
                if (value != USE_TYPE.NOT_USING)
                {
                    // Item currently not in use and we want to
                    // start using it, so start using it
                    if (equipped.on_use_start(value).underway)
                        _current_item_use = value; // Needs continuing
                    else
                        _current_item_use = USE_TYPE.NOT_USING; // Immediately completed
                }
            }
            else
            {
                if (value == USE_TYPE.NOT_USING)
                {
                    // Item currently in use and we want to stop
                    // using it, so stop using it
                    equipped.on_use_end(value);
                    _current_item_use = value;
                }
            }
        }
    }

    // The current equipped item
    item _equipped;
    public item equipped { get => _equipped; }

    void equip(string item_name)
    {
        if (_equipped != null && _equipped.name == item_name)
            return; // Already equipped

        if (_equipped != null)
            Destroy(_equipped.gameObject);

        if (item_name == null)
        {
            _equipped = null;
        }
        else
        {
            // Ensure we actually have one of these in my inventory
            bool have = false;
            foreach (var s in inventory.slots)
                if (s.item == item_name)
                {
                    have = true;
                    break;
                }

            if (have)
            {
                // Create an equipped-type copy of the item
                _equipped = item.create(item_name, transform.position, transform.rotation);
                foreach (var c in _equipped.GetComponentsInChildren<Collider>())
                    Destroy(c);
            }
            else _equipped = null; // Don't have, equip null
        }

        if (_equipped == null)
            cursor = cursors.DEFAULT;
        else
        {
            cursor = _equipped.sprite.name;
            _equipped.transform.SetParent(hand);
            _equipped.transform.localPosition = Vector3.zero;
            _equipped.transform.localRotation = Quaternion.identity;
        }
    }

    public void re_equip()
    {
        // Re-equip the current item if we still have one in inventory
        if (equipped == null) equip(null);
        else
        {
            string re_eq_name = equipped.name;
            Destroy(equipped.gameObject);
            equip(null);
            equip(re_eq_name);
        }
    }

    void toggle_equip(string item)
    {
        if (equipped?.name == item) equip(null);
        else equip(item);
    }

    void run_quickbar_shortcuts()
    {
        // Select quickbar item using keyboard shortcut
        if (Input.GetKeyDown(KeyCode.Alpha1)) toggle_equip(quickbar_slot(0)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) toggle_equip(quickbar_slot(1)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) toggle_equip(quickbar_slot(2)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) toggle_equip(quickbar_slot(3)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha5)) toggle_equip(quickbar_slot(4)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha6)) toggle_equip(quickbar_slot(5)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha7)) toggle_equip(quickbar_slot(6)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha8)) toggle_equip(quickbar_slot(7)?.item);

        // Scroll through quickbar items
        float sw = Input.GetAxis("Mouse ScrollWheel");

        if (sw != 0)
        {
            for (int attempt = 0; attempt < QUICKBAR_SLOTS_COUNT; ++attempt)
            {
                if (sw > 0) ++last_quickbar_slot_accessed;
                else if (sw < 0) --last_quickbar_slot_accessed;
                if (last_quickbar_slot_accessed < 0) last_quickbar_slot_accessed = QUICKBAR_SLOTS_COUNT - 1;
                last_quickbar_slot_accessed = last_quickbar_slot_accessed % QUICKBAR_SLOTS_COUNT;

                var itm = quickbar_slot(last_quickbar_slot_accessed)?.item;
                if (itm != null)
                {
                    equip(itm);
                    break;
                }
            }
        }
    }

    //###########//
    //  MOVEMENT //
    //###########//

    CharacterController controller;
    Vector3 velocity = Vector3.zero;

    void move()
    {
        if (controller == null) return; // Controller hasn't started yet

        if (controller.isGrounded)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                velocity.y = JUMP_VEL;
        }
        else velocity.y -= GRAVITY * Time.deltaTime;

        if (Input.GetKey(KeyCode.W)) velocity += transform.forward * ACCELERATION * Time.deltaTime;
        else if (Input.GetKey(KeyCode.S)) velocity -= transform.forward * ACCELERATION * Time.deltaTime;
        else velocity -= Vector3.Project(velocity, transform.forward);

        if (map_open)
        {
            if (Input.GetKey(KeyCode.D)) y_rotation.value += ROTATION_SPEED * Time.deltaTime;
            else if (Input.GetKey(KeyCode.A)) y_rotation.value -= ROTATION_SPEED * Time.deltaTime;
            else velocity -= Vector3.Project(velocity, camera.transform.right);
        }
        else
        {
            if (Input.GetKey(KeyCode.D)) velocity += camera.transform.right * ACCELERATION * Time.deltaTime;
            else if (Input.GetKey(KeyCode.A)) velocity -= camera.transform.right * ACCELERATION * Time.deltaTime;
            else velocity -= Vector3.Project(velocity, camera.transform.right);
        }

        float xz = new Vector3(velocity.x, 0, velocity.z).magnitude;
        if (xz > SPEED)
        {
            velocity.x *= SPEED / xz;
            velocity.z *= SPEED / xz;
        }

        Vector3 move = velocity * Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            move.x *= 10f;
            move.z *= 10f;
        }

        controller.Move(move);
        stay_above_terrain();

        networked_position = transform.position;
    }

    void float_in_water()
    {
        // We're underwater if the bottom of the screen is underwater
        var ray = camera.ScreenPointToRay(new Vector3(Screen.width / 2f, 0, 0));
        float dis = camera.nearClipPlane / Vector3.Dot(ray.direction, -camera.transform.up);
        float eff_eye_y = (ray.origin + ray.direction * dis).y;
        underwater_screen.SetActive(eff_eye_y < world.SEA_LEVEL && !map_open);

        float amt_submerged = (world.SEA_LEVEL - transform.position.y) / HEIGHT;
        if (amt_submerged > 1.0f) amt_submerged = 1.0f;
        if (amt_submerged <= 0) return;

        // Bouyancy (sink if shift is held)
        if (!Input.GetKey(KeyCode.LeftShift))
            velocity.y += amt_submerged * (GRAVITY + BOUYANCY) * Time.deltaTime;

        // Drag
        velocity -= velocity * amt_submerged * WATER_DRAG * Time.deltaTime;
    }

    void stay_above_terrain()
    {
        Vector3 pos = transform.position;
        pos.y = world.MAX_ALTITUDE;
        RaycastHit hit;
        var tc = utils.raycast_for_closest<TerrainCollider>(new Ray(pos, Vector3.down), out hit);
        if (hit.point.y > transform.position.y)
            transform.position = hit.point;
    }

    //#####################//
    // VIEW/CAMERA CONTROL //
    //#####################//

    // Objects used to obscure player view
    public new Camera camera { get; private set; }
    GameObject obscurer;
    GameObject map_obscurer;
    GameObject underwater_screen;

    // Called when the render range changes
    public void update_render_range()
    {
        // Let the network know
        render_range = game.render_range;

        // Set the obscurer size to the render range
        obscurer.transform.localScale = Vector3.one * game.render_range * 0.99f;
        map_obscurer.transform.localScale = Vector3.one * game.render_range;

        if (!map_open)
        {
            // If in 3D mode, set the camera clipping plane range to
            // the same as render_range
            camera.farClipPlane = game.render_range;
            QualitySettings.shadowDistance = camera.farClipPlane;
        }
    }

    void mouse_look()
    {
        // Rotate the view using the mouse
        // Note that horizontal moves rotate the player
        // vertical moves rotate the camera
        float yr = Input.GetAxis("Mouse X") * 5f;
        if (yr != 0) y_rotation.value += yr;
        camera.transform.Rotate(-Input.GetAxis("Mouse Y") * 5, 0, 0);
    }

    // Saved rotation to restore when we return to the 3D view
    Quaternion saved_camera_rotation;

    // True if in map view
    public bool map_open
    {
        get { return camera.orthographic; }
        set
        {
            // Use the appropriate obscurer for
            // the map or 3D views
            map_obscurer.SetActive(value);
            obscurer.SetActive(!value);

            // Set the camera orthograpic if in 
            // map view, otherwise perspective
            camera.orthographic = value;

            if (value)
            {
                // Save camera rotation to restore later
                saved_camera_rotation = camera.transform.localRotation;

                // Setup the camera in map mode/position   
                camera.orthographicSize = game.render_range;
                camera.transform.localPosition = Vector3.up * (MAP_CAMERA_ALT - transform.position.y);
                camera.transform.localRotation = Quaternion.Euler(90, 0, 0);
                camera.farClipPlane = MAP_CAMERA_CLIP;

                // Render shadows further in map view
                QualitySettings.shadowDistance = MAP_SHADOW_DISTANCE;
            }
            else
            {
                // Restore 3D camera view
                camera.transform.localPosition = Vector3.up * (HEIGHT - WIDTH / 2f);
                camera.transform.localRotation = saved_camera_rotation;
            }
        }
    }

    // Return a ray going through the centre of the screen
    public Ray camera_ray()
    {
        return new Ray(camera.transform.position,
                       camera.transform.forward);
    }

    //#################//
    // PLAYER CREATION //
    //#################//

    public override void on_create()
    {
        // Load the player body
        var body = Resources.Load<GameObject>("misc/player_body").inst();
        body.transform.SetParent(transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;

        // Scale the player body so the eyes are at the correct height
        var eye_level = body.transform.Find("eye_level");
        float eye_y = (eye_level.transform.position - transform.position).y;
        body.transform.localScale *= (HEIGHT - WIDTH / 2f) / eye_y;

        if (local)
        {
            // Setup the player camera
            camera = FindObjectOfType<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.SetParent(transform);
            camera.transform.localPosition = new Vector3(0, HEIGHT - WIDTH / 2f, 0);
            camera.nearClipPlane = 0.1f;

            // Enforce the render limit with a sky-color object
            obscurer = Resources.Load<GameObject>("misc/obscurer").inst();
            obscurer.transform.SetParent(transform);
            obscurer.transform.localPosition = Vector3.zero;
            var sky_color = obscurer.GetComponentInChildren<Renderer>().material.color;

            map_obscurer = Resources.Load<GameObject>("misc/map_obscurer").inst();
            map_obscurer.transform.SetParent(camera.transform);
            map_obscurer.transform.localPosition = Vector3.forward;
            map_obscurer.transform.up = -camera.transform.forward;

            // The distance to the underwater screen, just past the near clipping plane
            float usd = camera.nearClipPlane * 1.1f;
            Vector3 bl_corner_point = camera.ScreenToWorldPoint(new Vector3(0, 0, usd));
            Vector3 tr_corner_point = camera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, usd));
            Vector3 delta = tr_corner_point - bl_corner_point;

            // Setup the underwater screen so it exactly covers the screen
            underwater_screen = Resources.Load<GameObject>("misc/underwater_screen").inst();
            underwater_screen.transform.SetParent(camera.transform);
            underwater_screen.transform.localPosition = Vector3.forward * usd;
            underwater_screen.transform.localScale = new Vector3(
                Vector3.Dot(delta, camera.transform.right),
                Vector3.Dot(delta, camera.transform.up),
                1f
            ) * 1.01f; // 1.01f factor to ensure that it covers the screen
            underwater_screen.transform.forward = camera.transform.forward;

            // Make the sky the same color as the obscuring object
            RenderSettings.skybox = null;
            camera.backgroundColor = sky_color;

            // Set the hand location so it is one meter
            // away from the camera, 80% of the way across 
            // the screen and 10% of the way up the screen.
            hand = new GameObject("hand").transform;
            hand.SetParent(camera.transform);
            var r = camera.ScreenPointToRay(new Vector3(
                 Screen.width * 0.8f,
                 Screen.height * 0.1f
                 ));
            hand.localPosition = r.direction * 0.75f;

            // Create the crosshairs
            crosshairs = new GameObject("corsshairs").AddComponent<UnityEngine.UI.Image>();
            crosshairs.transform.SetParent(FindObjectOfType<Canvas>().transform);
            crosshairs.color = new Color(1, 1, 1, 0.5f);
            var crt = crosshairs.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(64, 64);
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            cursor = "default_cursor";

            // Initialize the render range
            update_render_range();

            // Start with the map closed
            map_open = false;

            // Start looking to add the player controller
            Invoke("add_controller", 0.1f);

            // This is the local player
            current = this;

            game.on_local_player_create(this);
        }

        // Initialize the inventory to closed
        inventory = Resources.Load<inventory>("ui/player_inventory").inst();
        inventory.name = "player inventory (" + name + ")";
        inventory.transform.SetParent(FindObjectOfType<Canvas>().transform);
        inventory.transform.position = new Vector3(Screen.width / 2, Screen.height / 2, 0);
        inventory_open = false;

        // Player starts with an axe
        inventory.add("axe", 1);
        inventory.add("furnace", 1);
    }

    void add_controller()
    {
        var c = chunk.at(transform.position, true);
        if (c == null)
        {
            // Wait until the chunk has generated
            Invoke("add_controller", 0.1f);
            return;
        }

        // Add the player controller once everything has loaded
        // (stops the controller from snapping us back to 0,0,0 and
        //  ensures the geometry we're standing on is properly loaded)
        controller = gameObject.AddComponent<CharacterController>();
        controller.height = HEIGHT;
        controller.radius = WIDTH / 2;
        controller.center = new Vector3(0, controller.height / 2f, 0);
        controller.skinWidth = controller.radius / 10f;
        controller.slopeLimit = 60f;
    }

    //############//
    // NETWORKING //
    //############//

    networked_variable.net_float y_rotation;
    public networked_variable.net_string username;

    public override void on_init_network_variables()
    {
        y_rotation = new networked_variable.net_float(resolution: 5f);
        y_rotation.on_change = (yrot) =>
        {
            transform.rotation = Quaternion.Euler(0, yrot, 0);
        };

        username = new networked_variable.net_string();
    }

    //################//
    // STATIC METHODS //
    //################//

    // The current (local) player
    public static player current;

    public static string info()
    {
        if (current == null) return "No local player";
        return "Local player " + current.username.value + " at " +
            System.Math.Round(current.transform.position.x, 1) + " " +
            System.Math.Round(current.transform.position.y, 1) + " " +
            System.Math.Round(current.transform.position.z, 1);
    }
}

public static class cursors
{
    public const string DEFAULT = "default_cursor";
    public const string DEFAULT_INTERACTION = "default_interact_cursor";
    public const string GRAB_OPEN = "default_interact_cursor";
    public const string GRAB_CLOSED = "grab_closed_cursor";
}

public class popup_message : MonoBehaviour
{
    // The scroll speed of a popup message
    // (in units of the screen height)
    public const float SCREEN_SPEED = 0.05f;

    new RectTransform transform;
    UnityEngine.UI.Text text;
    float start_time;

    public static popup_message create(string message)
    {
        var m = new GameObject("message").AddComponent<popup_message>();
        m.text = m.gameObject.AddComponent<UnityEngine.UI.Text>();

        m.transform = m.GetComponent<RectTransform>();
        m.transform.SetParent(FindObjectOfType<Canvas>().transform);
        m.transform.anchorMin = new Vector2(0.5f, 0.25f);
        m.transform.anchorMax = new Vector2(0.5f, 0.25f);
        m.transform.anchoredPosition = Vector2.zero;

        m.text.font = Resources.Load<Font>("fonts/monospace");
        m.text.text = message;
        m.text.alignment = TextAnchor.MiddleCenter;
        m.text.verticalOverflow = VerticalWrapMode.Overflow;
        m.text.horizontalOverflow = HorizontalWrapMode.Overflow;
        m.text.fontSize = 32;
        m.start_time = Time.realtimeSinceStartup;

        return m;
    }

    private void Update()
    {
        transform.position +=
            Vector3.up * Screen.height *
            SCREEN_SPEED * Time.deltaTime;

        float time = Time.realtimeSinceStartup - start_time;

        text.color = new Color(1, 1, 1, 1 - time);

        if (time > 1)
            Destroy(this.gameObject);
    }
}