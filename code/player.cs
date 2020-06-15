using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player : networked_player
{
    //###########//
    // CONSTANTS //
    //###########//

    // Size + kinematics
    public const float HEIGHT = 1.5f;
    public const float WIDTH = 0.45f;
    public const float GRAVITY = 10f;
    public const float BOUYANCY = 5f;
    public const float WATER_DRAG = 1.5f;

    // Movement
    public const float BASE_SPEED = 7f;
    public const float ACCELERATION_TIME = 0.2f;
    public const float ACCELERATION = BASE_SPEED / ACCELERATION_TIME;
    public const float ROTATION_SPEED = 90f;
    public const float JUMP_VEL = 5f;
    public const float THROW_VELOCITY = 6f;

    // Where does the hand appear
    public const float BASE_EYE_TO_HAND_DIS = 0.3f;
    public const float HAND_SCREEN_X = 0.9f;
    public const float HAND_SCREEN_Y = 0.1f;

    // How far away can we interact with things
    public const float INTERACTION_RANGE = 3f;

    // Map camera setup
    public const float MAP_CAMERA_ALT = world.MAX_ALTITUDE * 2;
    public const float MAP_CAMERA_CLIP = world.MAX_ALTITUDE * 3;
    public const float MAP_SHADOW_DISTANCE = world.MAX_ALTITUDE * 3;

    //#################//
    // UNITY CALLBACKS //
    //#################//

    public biome biome { get; private set; }

    /// <summary> Update function that is only called on the local client. </summary>
    void local_update()
    {
        if (options_menu.open) return;

        inspect_info.visible = Input.GetKey(KeyCode.Tab);

        if (Input.GetKeyDown(KeyCode.F1))
            cinematic_mode = !cinematic_mode;

        biome = biome.at(transform.position);
        if (biome != null)
        {
            var point_at = biome.blended_point(transform.position);
            target_sky_color = point_at.sky_color;
            water.color = point_at.water_color;
        }

        sky_color = Color.Lerp(sky_color, target_sky_color, Time.deltaTime * 5f);

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

                // Open/close the left menu
                if (inventory_open)
                {
                    var ray = camera_ray(INTERACTION_RANGE, out float dist);
                    left_menu = utils.raycast_for_closest<ILeftPlayerMenu>(ray, out RaycastHit hit, dist);
                }
                else left_menu = null;
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
        else if (Input.GetKeyDown(KeyCode.V))
            first_person = !first_person;

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
                            equipped.transform.rotation,
                            kinematic: false,
                            networked: true);
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

    void Update()
    {
        // Call the local update function
        if (has_authority)
            local_update();

        // Allign the arm with the hand
        right_arm.to_grab = equipped?.transform;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, game.render_range);

        Gizmos.color = Color.green;
        float dis;
        var r = camera_ray(INTERACTION_RANGE, out dis);
        Gizmos.DrawLine(r.origin, r.origin + r.direction * dis);
    }

    //###########//
    // INVENTORY //
    //###########//

    public armour_piece get_armour(armour_piece.LOCATION location)
    {
        foreach (var al in GetComponentsInChildren<armour_locator>())
            if (al.location == location)
                return al.equipped;
        return null;
    }

    public void set_armour(armour_piece armour, armour_piece.HANDEDNESS slot_handedness)
    {
        foreach (var al in GetComponentsInChildren<armour_locator>())
            if (al.location == armour.location && al.handedness == slot_handedness)
                al.equipped = armour;
    }

    public void clear_armour(armour_piece.LOCATION location, armour_piece.HANDEDNESS handedness)
    {
        foreach (var al in GetComponentsInChildren<armour_locator>())
            if (al.location == location && al.handedness == handedness)
                al.equipped = null;
    }

    const int QUICKBAR_SLOTS_COUNT = 8;

    public inventory inventory { get => GetComponentInChildren<inventory>(); }

    /// <summary> The menu that appears to the left of the inventory. </summary>
    ILeftPlayerMenu left_menu
    {
        get => _left_menu;
        set
        {
            if (_left_menu == value)
                return; // No change

            if (value != null)
            {
                // Position the left menu at the left_expansion_point
                // but leave it parented to the canvas, rather than
                // the player inventory
                var rt = value.left_menu_transform();
                rt.gameObject.SetActive(true);
                rt.SetParent(inventory.contents.left_expansion_point);
                rt.anchoredPosition = Vector2.zero;
                rt.SetParent(FindObjectOfType<game>().main_canvas.transform);
                value.on_left_menu_open();
            }

            if (_left_menu != null)
            {
                // Deactivate the menu
                _left_menu.on_left_menu_close();
                _left_menu.left_menu_transform().gameObject.SetActive(false);
            }

            _left_menu = value;
        }
    }
    ILeftPlayerMenu _left_menu;

    bool inventory_open
    {
        get { return inventory.ui_open; }
        set { inventory.ui_open = value; }
    }

    int last_quickbar_slot_accessed = 0;
    public inventory_slot quickbar_slot(int n)
    {
        n -= 1; // Move to zero-offset array
        if (inventory?.slots == null) return null;
        if (n < 0 || n >= inventory.slots.Length) return null;
        last_quickbar_slot_accessed = n;
        return inventory.slots[n];
    }

    public void close_all_ui()
    {
        if (inventory != null) inventory_open = false;
        left_menu = null;
    }

    //##########//
    // ITEM USE //
    //##########//

    // Called on a left click when no item is equipped
    public void left_click_with_hand()
    {
        float dis;
        var ray = camera_ray(INTERACTION_RANGE, out dis);

        // First, attempt to pick up items
        RaycastHit hit;
        item clicked = utils.raycast_for_closest<item>(ray, out hit, dis);
        if (clicked != null)
        {
            clicked.pick_up();
            return;
        }

        // Then attempt to pick items by hand
        var pick_by_hand = utils.raycast_for_closest<harvest_by_hand>(ray, out hit, dis);
        if (pick_by_hand != null)
        {
            pick_by_hand.on_pick();
            return;
        }
    }

    // Called on a right click when no item is equipped
    public void right_click_with_hand()
    {

    }

    UnityEngine.UI.Image crosshairs;
    public string cursor
    {
        get
        {
            if (crosshairs == null ||
                crosshairs.sprite == null ||
                !crosshairs.gameObject.activeInHierarchy) return null;
            return crosshairs.sprite.name;
        }
        set
        {
            if (cursor == value) return;
            if (value == null)
            {
                crosshairs.gameObject.SetActive(false);
            }
            else
            {
                crosshairs.gameObject.SetActive(true);
                crosshairs.sprite = Resources.Load<Sprite>("sprites/" + value);
            }
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
    public item equipped
    {
        get => _equipped;
        private set
        {
            if (_equipped == value)
                return; // Already equipped

            if (_equipped != null)
                Destroy(_equipped.gameObject);

            if (value == null)
            {
                _equipped = null;
            }
            else
            {
                // Ensure we actually have one of these in my inventory
                bool have = false;
                foreach (var s in inventory.slots)
                    if (s.item == value.name)
                    {
                        have = true;
                        break;
                    }

                if (have)
                {
                    // Create an equipped-type copy of the item
                    _equipped = item.create(value.name, transform.position, transform.rotation);
                    foreach (var c in _equipped.GetComponentsInChildren<Collider>())
                        c.enabled = false;

                    if (_equipped is equip_in_hand)
                    {
                        // This item can be eqipped in my hand
                    }
                    else
                    {
                        // This item can't be equipped in my hand, make it invisible.
                        foreach (var r in _equipped.GetComponentsInChildren<Renderer>())
                            r.enabled = false;
                    }
                }
                else _equipped = null; // Don't have, equip null
            }

            if (_equipped != null)
            {
                // Parent the equipped object to the hand
                _equipped.transform.SetParent(hand_centre);
                _equipped.transform.localPosition = Vector3.zero;
                _equipped.transform.localRotation = Quaternion.identity;
            }

            // If this is the local player, set the cursor
            if (has_authority)
                cursor = _equipped == null ? cursors.DEFAULT : _equipped.sprite.name;
        }
    }

    public void re_equip()
    {
        equipped = Resources.Load<item>("items/" + equipped.name);
    }

    void toggle_equip(int slot)
    {
        if (slot_equipped.value == slot) slot_equipped.value = -1;
        else slot_equipped.value = slot;
    }

    void run_quickbar_shortcuts()
    {
        // Select quickbar item using keyboard shortcut
        if (Input.GetKeyDown(KeyCode.Alpha1)) toggle_equip(1);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) toggle_equip(2);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) toggle_equip(3);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) toggle_equip(4);
        else if (Input.GetKeyDown(KeyCode.Alpha5)) toggle_equip(5);
        else if (Input.GetKeyDown(KeyCode.Alpha6)) toggle_equip(6);
        else if (Input.GetKeyDown(KeyCode.Alpha7)) toggle_equip(7);
        else if (Input.GetKeyDown(KeyCode.Alpha8)) toggle_equip(8);

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

                if (quickbar_slot(last_quickbar_slot_accessed)?.item != null)
                {
                    toggle_equip(last_quickbar_slot_accessed);
                    break;
                }
            }
        }
    }

    //############//
    // INSPECTION //
    //############//

    inspect_info inspect_info
    {
        get
        {
            // Create the inspect_info object if it doesn't already exist
            if (_inspect_info == null)
            {
                _inspect_info = Resources.Load<inspect_info>("ui/inspect_info").inst();
                _inspect_info.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
                _inspect_info.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            }

            return _inspect_info;
        }
    }
    inspect_info _inspect_info;

    //###########//
    //  MOVEMENT //
    //###########//

    CharacterController controller;
    Vector3 velocity = Vector3.zero;

    public float speed
    {
        get
        {
            float s = BASE_SPEED;
            if (crouched) s /= 2f;
            else if (Input.GetKey(KeyCode.LeftControl)) s /= 10f;

            return s;
        }
    }

    public bool crouched
    {
        get => _crouched;
        private set
        {
            // Can't crouch in cinematic mode
            if (cinematic_mode) value = false;

            _crouched = value;
            if (value) body.transform.localPosition = new Vector3(0, -0.25f, 0);
            else body.transform.localPosition = Vector3.zero;
        }
    }
    bool _crouched;

    bool cinematic_mode
    {
        get => _cinematic_mode;
        set
        {
            _cinematic_mode = value;

            fly_velocity = Vector3.zero;
            mouse_look_velocity = Vector2.zero;

            if (value)
            {
                close_all_ui();
                cursor = null;
            }
            else
            {
                cursor = cursors.DEFAULT;
            }

            // Make the player (in)visible
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                // Doesn't affect the obscurers/water
                if (r.transform.IsChildOf(obscurer.transform)) continue;
                if (r.transform.IsChildOf(map_obscurer.transform)) continue;
                if (r.transform.IsChildOf(water.transform)) continue;
                r.enabled = !value;
            }
        }
    }
    bool _cinematic_mode;

    void move()
    {
        if (controller == null) return; // Controller hasn't started yet

        if (Input.GetKeyDown(KeyCode.LeftShift)) crouched = true;
        if (Input.GetKeyUp(KeyCode.LeftShift)) crouched = false;

        if (cinematic_mode)
            fly_move();
        else
            normal_move();

        // Ensure we don't accumulate too much -ve y velocity
        if (controller.isGrounded && velocity.y < -1f)
            velocity.y = -1f;

        networked_position = transform.position;
    }

    void normal_move()
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
        if (xz > speed)
        {
            velocity.x *= speed / xz;
            velocity.z *= speed / xz;
        }

        Vector3 move = velocity * Time.deltaTime;

        controller.Move(move);
        stay_above_terrain();
    }

    Vector3 fly_velocity = Vector3.zero;
    void fly_move()
    {
        const float FLY_ACCEL = 10f;

        Vector3 fw = map_open ? transform.forward : camera.transform.forward;
        Vector3 ri = camera.transform.right;

        if (Input.GetKey(KeyCode.W)) fly_velocity += fw * 2 * FLY_ACCEL * Time.deltaTime;
        if (Input.GetKey(KeyCode.S)) fly_velocity -= fw * 2 * FLY_ACCEL * Time.deltaTime;
        if (Input.GetKey(KeyCode.D)) fly_velocity += ri * 2 * FLY_ACCEL * Time.deltaTime;
        if (Input.GetKey(KeyCode.A)) fly_velocity -= ri * 2 * FLY_ACCEL * Time.deltaTime;
        if (Input.GetKey(KeyCode.Space)) fly_velocity += Vector3.up * 2 * FLY_ACCEL * Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftShift)) fly_velocity -= Vector3.up * 2 * FLY_ACCEL * Time.deltaTime;

        if (fly_velocity.magnitude > FLY_ACCEL * Time.deltaTime)
            fly_velocity -= fly_velocity.normalized * FLY_ACCEL * Time.deltaTime;
        else
            fly_velocity = Vector3.zero;

        Vector3 move = fly_velocity * Time.deltaTime;
        controller.Move(move);
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

    /// <summary> The player camera. </summary>
#if UNITY_EDITOR
    new
#endif
    public Camera camera
    { get; private set; }

    // Objects used to obscure player view
    GameObject obscurer;
    GameObject map_obscurer;
    GameObject underwater_screen;

    Color target_sky_color;

    Color sky_color
    {
        get => utils.get_color(obscurer_renderer.material);
        set
        {
            camera.backgroundColor = value;
            utils.set_color(obscurer_renderer.material, value);
            utils.set_color(map_obscurer_renderer.material, value);
        }
    }
    Renderer obscurer_renderer;
    Renderer map_obscurer_renderer;

    /// <summary> Are we in first, or third person? </summary>
    public bool first_person
    {
        get => _first_person;
        private set
        {
            if (map_open)
                return; // Can't change perspective if the map is open

            _first_person = value;

            camera.transform.position = eye_transform.position;

            if (!_first_person)
            {
                // Move the camera back and slightly to the left
                camera.transform.position -= camera.transform.forward * 2f;
                camera.transform.position -= camera.transform.right * 0.25f;
            }
        }
    }
    bool _first_person;

    // The position of the hand when carrying an item at rest
    public Transform hand_centre { get; private set; }

    /// <summary> The initial distance that the hand is from the camera,
    /// along the camera.forward direction. </summary>
    float init_hand_plane;

    void mouse_look_normal()
    {
        // Rotate the view using the mouse
        // Note that horizontal moves rotate the player
        // vertical moves rotate the camera

        // y rotation of player controlled by left/right of mouse
        float yr = Input.GetAxis("Mouse X") * 5f;
        if (yr != 0) y_rotation.value += yr;

        eye_transform.Rotate(-Input.GetAxis("Mouse Y") * 5f, 0, 0);

        // Move the hand away from the eye if we're looking down
        var hc = hand_centre.localPosition;
        hc.z = init_hand_plane;
        if (eye_transform.forward.y < 0)
        {
            float down_amt = Mathf.Abs(eye_transform.forward.y);
            down_amt /= eye_transform.forward.magnitude;
            hc.z *= 1f + down_amt * 2.5f;
        }
        hand_centre.localPosition = hc;
    }

    Vector2 mouse_look_velocity = Vector2.zero;
    void mouse_look_fly()
    {
        // Smooth mouse look for fly mode
        mouse_look_velocity.x += Input.GetAxis("Mouse X");
        mouse_look_velocity.y += Input.GetAxis("Mouse Y");

        float deccel = Time.deltaTime * 10f;
        if (mouse_look_velocity.magnitude < deccel)
            mouse_look_velocity = Vector2.zero;
        else
            mouse_look_velocity -= mouse_look_velocity.normalized * deccel;

        y_rotation.value += mouse_look_velocity.x * Time.deltaTime;
        eye_transform.Rotate(-mouse_look_velocity.y * Time.deltaTime * 5f, 0, 0);
    }

    void mouse_look()
    {
        if (cinematic_mode)
            mouse_look_fly();
        else
            mouse_look_normal();
    }

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
            // Scale camera clipping plane/shadow distance to work
            // with the new render range
            camera.farClipPlane = game.render_range * 1.5f;
            QualitySettings.shadowDistance = game.render_range;
        }
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
                camera.transform.position = eye_transform.transform.position +
                    Vector3.up * (MAP_CAMERA_ALT - transform.position.y);
                camera.transform.rotation = Quaternion.LookRotation(
                    Vector3.down, transform.forward
                    );
                camera.farClipPlane = MAP_CAMERA_CLIP;

                // Render shadows further in map view
                QualitySettings.shadowDistance = MAP_SHADOW_DISTANCE;
            }
            else
            {
                // Restore 3D camera view
                camera.transform.localRotation = saved_camera_rotation;
                first_person = first_person; // This is needed
            }
        }
    }

    // Return a ray going through the centre of the screen
    public Ray camera_ray()
    {
        return new Ray(camera.transform.position,
                       camera.transform.forward);
    }

    /// <summary> Returns a camera ray, starting at the first point on the ray
    /// within max_range_from_player of the player, and the distance
    /// along the ray where it leaves max_range_from_player. </summary>
    public Ray camera_ray(float max_range_from_player, out float distance)
    {
        var ray = camera_ray();

        // Solve for the intersection of the ray
        // with the sphere of radius range_from_player around the player
        Vector3 cvec = ray.origin - eye_transform.position;
        float b = 2 * Vector3.Dot(ray.direction, cvec);
        float c = cvec.sqrMagnitude - max_range_from_player * max_range_from_player;

        // No solutions
        if (4 * c >= b * b)
        {
            distance = 0;
            return ray;
        }

        // Two solutions (in + out)
        var interval = new float[]
        {
            (-b - Mathf.Sqrt(b*b-4*c))/2f,
            (-b + Mathf.Sqrt(b*b-4*c))/2f,
        };

        // If the camera is inside the sphere, the in solution
        // is behind the camera, move it forward to the camera
        if (cvec.magnitude < max_range_from_player)
            interval[0] = 0;

        distance = interval[1] - interval[0];
        return new Ray(ray.origin + interval[0] * ray.direction, ray.direction);
    }

    //#################//
    // PLAYER CREATION //
    //#################//

    Vector3 eye_centre { get => transform.position + Vector3.up * (HEIGHT - WIDTH / 2f) + transform.forward * 0.25f; }

    public Transform eye_transform { get; private set; }
    arm right_arm;
    arm left_arm;
    water_reflections water;

    public override void on_loose_authority()
    {
        throw new System.Exception("Authority should not be lost for players!");
    }

    bool first_gain_auth = true;
    public override void on_gain_authority()
    {
        if (!first_gain_auth)
            throw new System.Exception("Players should not gain authority more than once!");
        first_gain_auth = false;

        // Setup the player camera
        camera = FindObjectOfType<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.transform.SetParent(eye_transform);
        camera.transform.localRotation = Quaternion.identity;
        camera.transform.localPosition = Vector3.zero;
        camera.nearClipPlane = 0.1f;
        first_person = true; // Start with camera in 1st person position

        // Enforce the render limit with a sky-color object
        obscurer = Resources.Load<GameObject>("misc/obscurer").inst();
        obscurer.transform.SetParent(transform);
        obscurer.transform.localPosition = Vector3.zero;
        obscurer_renderer = obscurer.GetComponentInChildren<Renderer>();

        map_obscurer = Resources.Load<GameObject>("misc/map_obscurer").inst();
        map_obscurer.transform.SetParent(camera.transform);
        map_obscurer.transform.localRotation = Quaternion.identity;
        map_obscurer.transform.localPosition = Vector3.forward;
        map_obscurer_renderer = map_obscurer.GetComponentInChildren<Renderer>();

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

        // Create the crosshairs
        crosshairs = new GameObject("corsshairs").AddComponent<UnityEngine.UI.Image>();
        crosshairs.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
        crosshairs.color = new Color(1, 1, 1, 0.5f);
        var crt = crosshairs.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(64, 64);
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        cursor = "default_cursor";

        // Add the water
        water = new GameObject("water").AddComponent<water_reflections>();
        water.transform.SetParent(transform);
        water.transform.position = transform.position;
        water.transform.rotation = transform.rotation;

        // Ensure sky color is set properly
        sky_color = sky_color;

        // Initialize the render range
        update_render_range();

        // Start with the map closed, first person view
        map_open = false;

        // Start looking to add the player controller
        Invoke("add_controller", 0.1f);

        // This is the local player
        current = this;
    }

    public player_body body { get; private set; }

    public override void on_create()
    {
        // Load the player body
        body = Resources.Load<player_body>("misc/player_body").inst();
        body.transform.SetParent(transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;

        // Scale the player body so the eyes are at the correct height
        eye_transform = body.eye_centre;
        float eye_y = (eye_transform.transform.position - transform.position).y;
        body.transform.localScale *= (eye_centre - transform.position).magnitude / eye_y;

        // Get references to the arms
        right_arm = body.right_arm;
        left_arm = body.left_arm;

        // Create the hand at the position specified by the hand_init_pos object
        hand_centre = new GameObject("hand").transform;
        hand_centre.SetParent(eye_transform);
        hand_centre.transform.localRotation = Quaternion.identity;
        var init_pos = eye_transform.Find("hand_init_pos");
        hand_centre.transform.position = init_pos.position;
        Destroy(init_pos.gameObject);
        init_hand_plane = hand_centre.localPosition.z;
    }

    public override void on_first_create()
    {
        // Create the inventory
        client.create(transform.position, "misc/player_inventory", parent: this);
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

    networked_variables.net_int slot_equipped;
    networked_variables.net_float y_rotation;
    public networked_variables.net_string username;

    public override void on_init_network_variables()
    {
        slot_equipped = new networked_variables.net_int();
        slot_equipped.on_change = () =>
        {
            if (slot_equipped.value < 1) equipped = null;
            else equipped = Resources.Load<item>("items/" + quickbar_slot(slot_equipped.value)?.item);
        };

        y_rotation = new networked_variables.net_float(resolution: 5f);
        y_rotation.on_change = () =>
        {
            transform.rotation = Quaternion.Euler(0, y_rotation.value, 0);
        };

        username = new networked_variables.net_string();
    }

    public override void on_add_networked_child(networked child)
    {
        // Let the inventory know it belongs to this player
        if (child is inventory)
        {
            var inv = (inventory)child;
            inv.contents.belongs_to = this;
        }
    }

    //################//
    // STATIC METHODS //
    //################//

    // The current (local) player
    public static player current
    {
        get => _player;
        private set
        {
            if (_player != null)
                throw new System.Exception("Tried to overwrite player.current!");
            _player = value;
            _player.username.value = game.startup.username;
        }
    }
    static player _player;

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
        m.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
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

public interface ILeftPlayerMenu
{
    RectTransform left_menu_transform();
    void on_left_menu_close();
    void on_left_menu_open();
}