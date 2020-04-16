using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player : MonoBehaviour
{
    //###########//
    // CONSTANTS //
    //###########//

    public const float HEIGHT = 1.8f;
    public const float WIDTH = 0.45f;
    public const float GRAVITY = 10f;
    public const float BOUYANCY = 5f;
    public const float WATER_DRAG = 1.5f;

    public const float SPEED = 10f;
    public const float ACCELERATION_TIME = 0.2f;
    public const float ACCELERATION = SPEED / ACCELERATION_TIME;
    public const float ROTATION_SPEED = 90f;
    public const float JUMP_VEL = 5f;
    public const float THROW_VELOCITY = 6f;

    public const float INTERACTION_RANGE = 3f;

    public const float MAP_CAMERA_ALT = world.MAX_ALTITUDE * 2;
    public const float MAP_CAMERA_CLIP = world.MAX_ALTITUDE * 3;
    public const float MAP_SHADOW_DISTANCE = world.MAX_ALTITUDE * 3;

    //###############//
    // SERIALIZATION //
    //###############//

    public void save()
    {
        var floats = new float[]
        {
            transform.position.x,
            transform.position.y,
            transform.position.z
        };

        using (var fs = new System.IO.FileStream(world.save_folder() + "/player",
            System.IO.FileMode.Create, System.IO.FileAccess.Write))
        {
            for (int i = 0; i < floats.Length; ++i)
            {
                var float_bytes = System.BitConverter.GetBytes(floats[i]);
                fs.Write(float_bytes, 0, float_bytes.Length);
            }
        }
    }

    void load()
    {
        if (!System.IO.File.Exists(world.save_folder() + "/player")) return;

        var floats = new float[3];

        using (var fs = new System.IO.FileStream(world.save_folder() + "/player",
            System.IO.FileMode.Open, System.IO.FileAccess.Read))
        {
            byte[] float_bytes = new byte[sizeof(float)];
            for (int i = 0; i < floats.Length; ++i)
            {
                fs.Read(float_bytes, 0, float_bytes.Length);
                floats[i] = System.BitConverter.ToSingle(float_bytes, 0);
            }
        }

        Vector3 pos = new Vector3(floats[0], floats[1], floats[2]);
        transform.position = pos;
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    void Update()
    {
        // Toggle inventory on E
        if (Input.GetKeyDown(KeyCode.E))
        {
            inventory_open = !inventory_open;
            crosshairs.enabled = !inventory_open;
            Cursor.visible = inventory_open;
            Cursor.lockState = inventory_open ? CursorLockMode.None : CursorLockMode.Locked;
        }

        // Throw equiped on T
        if (Input.GetKeyDown(KeyCode.T))
            if (equipped_item != null)
            {
                inventory.remove(equipped_item, 1);
                var spawned = item.spawn(equipped_item, _equipped.transform.position, _equipped.transform.rotation);
                spawned.rigidbody.velocity += camera.transform.forward * THROW_VELOCITY;
                equipped_item = equipped_item; // Attempt to re-equip if there is another in my inventory
            }

        // Left click
        if (Input.GetMouseButtonDown(0))
        {
            if (equipped_item != null) _equipped.use_left_click();
            else left_click_with_hand();
        }

        // Right click
        if (Input.GetMouseButtonDown(1))
        {
            if (equipped_item != null) _equipped.use_right_click();
            else right_click_with_hand();
        }

        if (!map_open) run_quickbar_shortcuts();

        // Toggle the map view on M
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

        move();
        if (!map_open && !inventory_open) mouse_look();
        float_in_water();
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
    void left_click_with_hand()
    {
        if (carrying != null) { carrying = null; return; }

        RaycastHit hit;
        item clicked = utils.raycast_for_closest<item>(camera_ray(), out hit, INTERACTION_RANGE);
        if (clicked != null)
            clicked.pick_up();
    }

    // Called on a right click when no item is equipped
    void right_click_with_hand()
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
    Transform hand { get; set; }

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

    // The current equipped item
    item _equipped;
    public string equipped_item
    {
        get { return _equipped == null ? null : _equipped.name; }
        private set
        {
            if (_equipped != null)
                Destroy(_equipped.gameObject);

            if (value == null)
                _equipped = null;
            else
            {
                // Ensure we actually have one of these in my inventory
                bool have = false;
                foreach (var s in inventory.slots)
                    if (s.item == value)
                    {
                        have = true;
                        break;
                    }

                if (have)
                {
                    // Create an equipped-type copy of the item
                    _equipped = item.load_from_name(value).inst();
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
    }

    void toggle_equip(string item)
    {
        if (equipped_item == item) equipped_item = null;
        else equipped_item = item;
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
        if (sw > 0) ++last_quickbar_slot_accessed;
        else if (sw < 0) --last_quickbar_slot_accessed;
        last_quickbar_slot_accessed = last_quickbar_slot_accessed % QUICKBAR_SLOTS_COUNT;
        if (sw != 0) equipped_item = quickbar_slot(last_quickbar_slot_accessed)?.item;
    }

    //###########//
    //  MOVEMENT //
    //###########//

    CharacterController controller;
    Vector3 velocity = Vector3.zero;

    void move()
    {
        if (controller.isGrounded)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                velocity.y = JUMP_VEL;
        }
        else velocity.y -= GRAVITY * Time.deltaTime;

        if (Input.GetKey(KeyCode.W)) velocity += transform.forward * ACCELERATION * Time.deltaTime;
        else if (Input.GetKey(KeyCode.S)) velocity -= transform.forward * ACCELERATION * Time.deltaTime;
        else velocity -= Vector3.Project(velocity, transform.forward);

        if (Input.GetKey(KeyCode.D)) velocity += camera.transform.right * ACCELERATION * Time.deltaTime;
        else if (Input.GetKey(KeyCode.A)) velocity -= camera.transform.right * ACCELERATION * Time.deltaTime;
        else velocity -= Vector3.Project(velocity, camera.transform.right);

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
    }

    void float_in_water()
    {
        underwater_screen.SetActive(camera.transform.position.y < world.SEA_LEVEL && !map_open);

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
        if (map_open)
        {
            // Rotate the player with A/D
            float xr = 0;
            if (Input.GetKey(KeyCode.A)) xr = -1f;
            else if (Input.GetKey(KeyCode.D)) xr = 1.0f;
            transform.Rotate(0, xr * Time.deltaTime * ROTATION_SPEED, 0);
            return;
        }

        // Rotate the view using the mouse
        // Note that horizontal moves rotate the player
        // vertical moves rotate the camera
        transform.Rotate(0, Input.GetAxis("Mouse X") * 5, 0);
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

    void on_create()
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

        underwater_screen = Resources.Load<GameObject>("misc/underwater_screen").inst();
        underwater_screen.transform.SetParent(camera.transform);
        underwater_screen.transform.localPosition = Vector3.forward * camera.nearClipPlane * 1.1f;
        underwater_screen.transform.forward = camera.transform.forward;

        // Make the sky the same color as the obscuring object
        RenderSettings.skybox = null;
        camera.backgroundColor = sky_color;

        // Create the player controller
        controller = gameObject.AddComponent<CharacterController>();
        controller.height = HEIGHT;
        controller.radius = WIDTH / 2;
        controller.center = new Vector3(0, controller.height / 2f, 0);
        controller.skinWidth = controller.radius / 10f;

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

        // Initialize the inventory to closed
        inventory = Resources.Load<inventory>("ui/player_inventory").inst();
        inventory.transform.SetParent(FindObjectOfType<Canvas>().transform);
        inventory.transform.position = new Vector3(Screen.width / 2, Screen.height / 2, 0);
        inventory_open = false;

        inventory.add("axe", 1);
        inventory.add("log", 1024);
        inventory.add("planks", 1024);

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

        // Load the player state
        load();
    }

    //################//
    // STATIC METHODS //
    //################//

    // The current player
    public static player current;

    // Create and return a player
    public static player create()
    {
        var p = new GameObject("player").AddComponent<player>();
        p.on_create();
        current = p;
        return p;
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