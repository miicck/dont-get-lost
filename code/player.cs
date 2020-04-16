using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// An object that the player can interact with
public class interactable : MonoBehaviour
{
    [System.Flags]
    public enum FLAGS
    {
        NONE = 0,
        DISALLOWS_MOVEMENT = 2,
        DISALLOWS_ROTATION = 4,
    };

    public enum INTERACT_TYPE
    {
        LEFT_CLICK,
        RIGHT_CLICK
    };

    public virtual string cursor() { return cursors.DEFAULT_INTERACTION; }
    public virtual FLAGS player_interact() { return FLAGS.NONE; }
    public virtual void on_start_interaction(RaycastHit point_hit, item interact_with, INTERACT_TYPE type) { }
    public virtual void on_end_interaction() { }
    protected void stop_interaction() { player.current.interacting_with = null; }
}

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
            inventory_open = !inventory_open;
        Cursor.visible = inventory_open;
        Cursor.lockState = inventory_open ? CursorLockMode.None : CursorLockMode.Locked;
        if (inventory_open) return;

        // Throw equiped on T
        if (Input.GetKeyDown(KeyCode.T))
        {
            var eq = equipped_item;
            if (eq != null)
            {
                inventory.remove(equipped_item, 1);
                item.spawn(eq, camera.transform.position + camera.transform.forward);
                equipped_item = equipped_item; // Attempt to re-equip
            }
        }

        run_quickbar_shortcuts();

        var inter_flags = interact();

        // Toggle the map view
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

        if (inter_flags.HasFlag(interactable.FLAGS.DISALLOWS_MOVEMENT))
            velocity = Vector3.zero;
        else move();

        if (!inter_flags.HasFlag(interactable.FLAGS.DISALLOWS_ROTATION))
            mouse_look();

        // Float in water
        float amt_submerged = (world.SEA_LEVEL - transform.position.y) / HEIGHT;
        if (amt_submerged > 1.0f) amt_submerged = 1.0f;
        if (amt_submerged > 0)
        {
            // Bouyancy (sink if shift is held)
            if (!Input.GetKey(KeyCode.LeftShift))
                velocity.y += amt_submerged * (GRAVITY + BOUYANCY) * Time.deltaTime;

            // Drag
            velocity -= velocity * amt_submerged * WATER_DRAG * Time.deltaTime;
        }

        underwater_screen.SetActive(camera.transform.position.y < world.SEA_LEVEL && !map_open);

        // Use my tool
        if (interacting_with == null)
        {
            if (Input.GetMouseButtonDown(0))
                swing_tool();

            if (item_swing_progress < 1f)
            {
                item_swing_progress += Time.deltaTime / ITEM_SWING_TIME;

                float fw_amt = -Mathf.Sin(item_swing_progress * Mathf.PI * 2f);
                hand.transform.localPosition = init_hand_local_position +
                    fw_amt * Vector3.forward * ITEM_SWING_DISTANCE -
                    fw_amt * Vector3.up * ITEM_SWING_DISTANCE -
                    fw_amt * Vector3.right * init_hand_local_position.x;

                Vector3 up = camera.transform.up * (1 - fw_amt) + camera.transform.forward * fw_amt;
                Vector3 fw = -Vector3.Cross(up, camera.transform.right);
                hand.transform.rotation = Quaternion.LookRotation(fw, up);
            }
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

    const float ITEM_SWING_TIME = 0.5f;
    const float ITEM_SWING_DISTANCE = 0.25f;
    float item_swing_progress = 1f;
    Vector3 init_hand_local_position;

    // The hand which carries an item
    Transform hand { get; set; }

    void swing_tool()
    {
        item_swing_progress = 0f;
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

            if (value != null)
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

            if (_equipped != null)
            {
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
        if (Input.GetKeyDown(KeyCode.Alpha1)) toggle_equip(quickbar_slot(0)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) toggle_equip(quickbar_slot(1)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) toggle_equip(quickbar_slot(2)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) toggle_equip(quickbar_slot(3)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha5)) toggle_equip(quickbar_slot(4)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha6)) toggle_equip(quickbar_slot(5)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha7)) toggle_equip(quickbar_slot(6)?.item);
        else if (Input.GetKeyDown(KeyCode.Alpha8)) toggle_equip(quickbar_slot(7)?.item);

        float sw = Input.GetAxis("Mouse ScrollWheel");
        if (sw > 0) ++last_quickbar_slot_accessed;
        else if (sw < 0) --last_quickbar_slot_accessed;
        last_quickbar_slot_accessed = last_quickbar_slot_accessed % QUICKBAR_SLOTS_COUNT;
        if (sw != 0) equipped_item = quickbar_slot(last_quickbar_slot_accessed)?.item;
    }

    //##################//
    // ITEM INTERACTION //
    //##################//

    // The object we are currently interacting with
    RaycastHit last_interaction_hit;
    interactable _interacting_with;
    public interactable interacting_with
    {
        get { return _interacting_with; }
        set
        {
            if (_interacting_with != null)
                _interacting_with.on_end_interaction();

            _interacting_with = value;

            if (value != null)
            {
                if (Input.GetMouseButtonDown(0))
                    value.on_start_interaction(last_interaction_hit, _equipped,
                        interactable.INTERACT_TYPE.LEFT_CLICK);
                else if (Input.GetMouseButtonDown(1))
                    value.on_start_interaction(last_interaction_hit, _equipped,
                        interactable.INTERACT_TYPE.RIGHT_CLICK);
                else
                    Debug.LogError("Unkown interaction type!");
            }
        }
    }

    interactable.FLAGS interact()
    {
        // Interact with the current object
        if (interacting_with != null)
        {
            canvas.cursor = interacting_with.cursor();
            return interacting_with.player_interact();
        }

        // See if an interactable object is under the cursor
        var inter = utils.raycast_for_closest<interactable>(
            camera_ray(), out last_interaction_hit, INTERACTION_RANGE);

        if (inter == null)
        {
            canvas.cursor = cursors.DEFAULT;
            return interactable.FLAGS.NONE;
        }
        else canvas.cursor = cursors.DEFAULT_INTERACTION;

        // Set the interactable and cursor,
        // interact with the object
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            interacting_with = inter;

        return interactable.FLAGS.NONE;
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

    //################//
    // STATIC METHODS //
    //################//

    // The current player
    public static player current;

    // Create and return a player
    public static player create()
    {
        var p = new GameObject("player").AddComponent<player>();

        // Create the player camera 
        p.camera = FindObjectOfType<Camera>();
        p.camera.clearFlags = CameraClearFlags.SolidColor;
        p.camera.transform.SetParent(p.transform);
        p.camera.transform.localPosition = new Vector3(0, HEIGHT - WIDTH / 2f, 0);
        p.camera.nearClipPlane = 0.1f;
        //p.camera.gameObject.AddComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>();


        // Create a short range light with no shadows to light up detail
        // on nearby objects to the player
        /*
        var point_light = new GameObject("point_light").AddComponent<Light>();
        point_light.type = LightType.Point;
        point_light.range = item.WELD_RANGE;
        point_light.transform.SetParent(p.camera.transform);
        point_light.transform.localPosition = Vector3.zero;
        point_light.intensity = 0.5f;
        */

        // Enforce the render limit with a sky-color object
        p.obscurer = Resources.Load<GameObject>("misc/obscurer").inst();
        p.obscurer.transform.SetParent(p.transform);
        p.obscurer.transform.localPosition = Vector3.zero;
        var sky_color = p.obscurer.GetComponentInChildren<Renderer>().material.color;

        p.map_obscurer = Resources.Load<GameObject>("misc/map_obscurer").inst();
        p.map_obscurer.transform.SetParent(p.camera.transform);
        p.map_obscurer.transform.localPosition = Vector3.forward;
        p.map_obscurer.transform.up = -p.camera.transform.forward;

        p.underwater_screen = Resources.Load<GameObject>("misc/underwater_screen").inst();
        p.underwater_screen.transform.SetParent(p.camera.transform);
        p.underwater_screen.transform.localPosition = Vector3.forward * p.camera.nearClipPlane * 1.1f;
        p.underwater_screen.transform.forward = p.camera.transform.forward;

        // Make the sky the same color as the obscuring object
        RenderSettings.skybox = null;
        p.camera.backgroundColor = sky_color;

        // Initialize the render range
        p.update_render_range();

        // Start with the map closed
        p.map_open = false;

        // Load the player state
        p.load();

        // Create the player controller
        p.controller = p.gameObject.AddComponent<CharacterController>();
        p.controller.height = HEIGHT;
        p.controller.radius = WIDTH / 2;
        p.controller.center = new Vector3(0, p.controller.height / 2f, 0);
        p.controller.skinWidth = p.controller.radius / 10f;

        // Set the hand location so it is one meter
        // away from the camera, 80% of the way across 
        // the screen and 10% of the way up the screen.
        p.hand = new GameObject("hand").transform;
        p.hand.SetParent(p.camera.transform);
        var r = p.camera.ScreenPointToRay(new Vector3(
             Screen.width * 0.8f,
             Screen.height * 0.1f
             ));
        p.hand.localPosition = r.direction * 0.75f;
        p.init_hand_local_position = p.hand.localPosition;

        // Initialize the inventory to closed
        p.inventory = Resources.Load<inventory>("ui/player_inventory").inst();
        p.inventory.transform.SetParent(FindObjectOfType<Canvas>().transform);
        p.inventory.transform.position = new Vector3(Screen.width / 2, Screen.height / 2, 0);
        p.inventory_open = false;

        p.inventory.add("axe", 1);

        current = p;
        return p;
    }
}