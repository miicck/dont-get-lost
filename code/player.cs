using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player : networked_player, INotPathBlocking
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
    public const float BASE_SPEED = 4f;
    public const float ACCELERATION_TIME = 0.2f;
    public const float ACCELERATION = BASE_SPEED / ACCELERATION_TIME;
    public const float CROUCH_SPEED_MOD = 0.25f;
    public const float SLOW_WALK_SPEED_MOD = 0.05f;
    public const float ROTATION_SPEED = 90f;
    public const float JUMP_VEL = 5f;
    public const float THROW_VELOCITY = 6f;
    public const float LADDER_TEST_DISTANCE = 0.25f;
    public const float LADDER_SPEED_MULT = 0.5f;

    // Where does the hand appear
    public const float BASE_EYE_TO_HAND_DIS = 0.3f;
    public const float HAND_SCREEN_X = 0.9f;
    public const float HAND_SCREEN_Y = 0.1f;

    // How far away can we interact with things
    public const float INTERACTION_RANGE = 3f;

    // Map camera setup
    public const float MAP_CAMERA_ALT = world.MAX_ALTITUDE * 2;

    //#################//
    // UNITY CALLBACKS //
    //#################//

    /// <summary> Run the world generator around the current player position. </summary>
    void run_world_generator()
    {
        biome = biome.at(transform.position, generate: true);
        if (biome != null)
        {
            point = biome.blended_point(transform.position);
            lighting.sky_color_daytime = point.sky_color;
            lighting.fog_distance = point.fog_distance;
            water.color = point.water_color;
        }
    }

    /// <summary> Update function that is only called on the local client. </summary>
    void local_update()
    {
        indicate_damage();
        run_world_generator();
        run_recipe_book();
        run_inspect_info();
        run_inventory();
        run_quickbar_shortcuts();
        run_map();
        run_first_third_person();
        run_item_use();
        run_mouse_look();
        run_movement();
        run_crosshairs();
    }

    void Update()
    {
        // Don't do anything if the console is open
        if (console.open) return;

        // Call the local update function
        if (has_authority) local_update();

        set_hand_position();
    }

    private void OnDrawGizmosSelected()
    {
        if (camera == null) return;

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

    void run_inventory()
    {
        // Things that mean we can't use the inventory
        if (current_item_use != USE_TYPE.NOT_USING) return;
        if (map_open) return;
        if (cinematic_mode) return;
        if (inventory == null) return;
        if (crafting_menu == null) return;

        // Ensure inventory is closed when the opions menu is
        if (options_menu.open)
        {
            inventory.open = false;
            crafting_menu.open = false;
            return;
        }

        // Toggle inventory on E
        if (Input.GetKeyDown(KeyCode.E))
        {
            inventory.open = !inventory.open;
            crafting_menu.open = inventory.open;
            Cursor.visible = inventory.open;
            Cursor.lockState = inventory.open ? CursorLockMode.None : CursorLockMode.Locked;

            // Open/close the left menu
            if (inventory.open)
            {
                var ray = camera_ray(INTERACTION_RANGE, out float dist);
                left_menu = utils.raycast_for_closest<ILeftPlayerMenu>(ray, out RaycastHit hit, dist);
            }
            else left_menu = null;
        }
    }

    void run_recipe_book()
    {
        if (Input.GetKeyDown(KeyCode.R))
            recipe_book_open = !recipe_book_open;
    }

    bool recipe_book_open
    {
        get => recipe.recipe_book.gameObject.activeInHierarchy;
        set
        {
            recipe.recipe_book.gameObject.SetActive(value);
            Cursor.visible = value;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }

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
            {
                al.equipped = null;
                return;
            }

        string err = "Could not find armour_locator with the location ";
        err += location + " and handedness " + handedness;
        throw new System.Exception(err);
    }

    const int QUICKBAR_SLOTS_COUNT = 8;

    public inventory inventory { get; private set; }
    public inventory crafting_menu { get; private set; }

    /// <summary> The menu that appears to the left of the inventory. </summary>
    public ILeftPlayerMenu left_menu
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
                if (rt == null)
                    value = null;
                else
                {
                    rt.gameObject.SetActive(true);
                    var attach_point = inventory.ui.GetComponentInChildren<left_menu_attach_point>();
                    rt.SetParent(attach_point.transform);
                    rt.anchoredPosition = Vector2.zero;
                    rt.SetParent(FindObjectOfType<game>().main_canvas.transform);
                    value.on_left_menu_open();
                }
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

    public inventory_slot_networked quickbar_slot(int n)
    {
        // Move to zero-offset array
        return inventory?.nth_slot(n - 1);
    }

    public void close_all_ui()
    {
        if (inventory != null) inventory.open = false;
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

        // Then attempt to harvest by hand
        var pick_by_hand = harvest_by_hand.raycast(ray, out hit, dis);
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

    item.use_result current_item_use_result;

    void run_item_use()
    {
        // Things that disallow item use
        if (options_menu.open) return;
        if (inventory.open) return;
        if (map_open) return;

        // Use items if the inventory/map aren't open
        current_item_use_result = item.use_result.complete;
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
            if (equipped == null) current_item_use_result = item.use_result.complete;
            else current_item_use_result = equipped.on_use_continue(current_item_use);
            if (!current_item_use_result.underway) current_item_use = USE_TYPE.NOT_USING;
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
                if (inventory.contains(value))
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

    public void validate_equip()
    {
        // Unequip if item is no more
        if (equipped?.name != quickbar_slot(slot_equipped.value)?.item?.name)
            slot_equipped.value = 0;
    }

    void toggle_equip(int slot)
    {
        if (slot_equipped.value == slot) slot_equipped.value = 0;
        else slot_equipped.value = slot;
    }

    void run_quickbar_shortcuts()
    {
        // Things that mean we can't use the quickbar shortcuts
        if (Cursor.visible) return;
        if (cinematic_mode) return;
        if (map_open) return;
        if (current_item_use != USE_TYPE.NOT_USING) return;
        if (inventory == null) return;

        // Select something in the world from the quickbar
        if (Input.GetKeyDown(KeyCode.Q))
        {
            var ray = camera_ray(INTERACTION_RANGE, out float dist);
            var itm = utils.raycast_for_closest<item>(ray, out RaycastHit hit, max_distance: dist);
            if (itm != null)
            {
                for (int i = 1; i <= QUICKBAR_SLOTS_COUNT; ++i)
                    if (quickbar_slot(i)?.item?.name == itm.name)
                    {
                        slot_equipped.value = i;
                        return;
                    }
            }
        }

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
            int new_slot = slot_equipped.value;
            for (int attempt = 0; attempt < QUICKBAR_SLOTS_COUNT; ++attempt)
            {
                if (sw > 0) ++new_slot;
                else if (sw < 0) --new_slot;

                if (new_slot < 1) new_slot = QUICKBAR_SLOTS_COUNT;
                if (new_slot > QUICKBAR_SLOTS_COUNT) new_slot = 1;

                if (quickbar_slot(new_slot)?.item != null)
                {
                    slot_equipped.value = new_slot;
                    break;
                }
            }
        }
    }

    //############//
    // INSPECTION //
    //############//

    void run_inspect_info()
    {
        inspect_info.visible = Input.GetKey(KeyCode.Tab);
    }

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

    void run_movement()
    {
        // Things that disallow movement
        if (left_menu != null) return;
        if (!current_item_use_result.allows_move) return;

        move();
        float_in_water();
    }

    void move()
    {
        if (controller == null) return; // Controller hasn't started yet
        if (!controller.enabled)
        {
            // Controller is disabled, probably so the
            // player position can be controlled by something
            // else (like a teleport). Keep the velocity = 0
            // and don't move the character.
            velocity = Vector3.zero;
            return;
        }

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

    void float_in_water()
    {
        // We're underwater if the bottom of the screen is underwater
        var ray = camera.ScreenPointToRay(new Vector3(Screen.width / 2f, 0, 0));
        float dis = camera.nearClipPlane / Vector3.Dot(ray.direction, -camera.transform.up);
        float eff_eye_y = (ray.origin + ray.direction * dis).y;
        underwater = eff_eye_y < world.SEA_LEVEL && !map_open;

        float amt_submerged = (world.SEA_LEVEL - transform.position.y) / HEIGHT;
        if (amt_submerged <= 0) return;
        if (amt_submerged > 1.0f) amt_submerged = 1.0f;

        // Bouyancy (sink if shift is held)
        if (!Input.GetKey(KeyCode.LeftShift))
            velocity.y += amt_submerged * (GRAVITY + BOUYANCY) * Time.deltaTime;

        // Drag
        velocity -= velocity * amt_submerged * WATER_DRAG * Time.deltaTime;
    }

    void normal_move()
    {
        Vector3 move = Vector3.zero;

        // Climb ladders
        bool climbing_ladder = false;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.LeftShift))
            foreach (var hit in
            Physics.CapsuleCastAll(transform.position + Vector3.up * WIDTH / 2f,
                                    transform.position + Vector3.up * (HEIGHT - WIDTH / 2f),
                                    WIDTH / 2f, transform.forward, LADDER_TEST_DISTANCE))
            {
                var lad = hit.collider.GetComponentInParent<ladder>();
                if (lad != null)
                {
                    climbing_ladder = true;
                    velocity.y = speed * LADDER_SPEED_MULT;
                    if (Input.GetKey(KeyCode.LeftShift))
                        velocity.y = 0;
                }
            }

        if (controller.isGrounded)
        {
            // Jumping
            if (Input.GetKeyDown(KeyCode.Space))
                velocity.y = JUMP_VEL;
        }
        else
        {
            // Gravity
            if (!climbing_ladder)
                velocity.y -= GRAVITY * Time.deltaTime;
        }

        // Control forward/back velocity
        if (Input.GetKey(KeyCode.W)) velocity += transform.forward * ACCELERATION * Time.deltaTime;
        else if (Input.GetKey(KeyCode.S)) velocity -= transform.forward * ACCELERATION * Time.deltaTime;
        else velocity -= Vector3.Project(velocity, transform.forward);

        // Control left/right veloctiy
        if (Input.GetKey(KeyCode.D)) velocity += camera.transform.right * ACCELERATION * Time.deltaTime;
        else if (Input.GetKey(KeyCode.A)) velocity -= camera.transform.right * ACCELERATION * Time.deltaTime;
        else velocity -= Vector3.Project(velocity, camera.transform.right);

        move += velocity * Time.deltaTime;

        // Ensure speed in x-z plane does not exceed movement speed
        float xz = new Vector3(velocity.x, 0, velocity.z).magnitude;
        if (xz > speed)
        {
            velocity.x *= speed / xz;
            velocity.z *= speed / xz;
        }

        controller.Move(move);

        // Ensure we stay above the terrain
        Vector3 pos = transform.position;
        pos.y = world.MAX_ALTITUDE;
        RaycastHit terra_hit;
        var tc = utils.raycast_for_closest<TerrainCollider>(new Ray(pos, Vector3.down), out terra_hit);
        if (terra_hit.point.y > transform.position.y)
            transform.position = terra_hit.point;
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

    CharacterController controller;
    Vector3 velocity = Vector3.zero;

    public void teleport(Vector3 location)
    {
        if (controller == null)
            return;

        Debug.Log("Teleporting to " + location);
        controller.enabled = false;
        networked_position = location;
        chunk.add_generation_listener(location, () =>
        {
            controller.enabled = true;
        });
    }

    public float speed
    {
        get
        {
            float s = BASE_SPEED;
            if (crouched)
                s *= CROUCH_SPEED_MOD;
            else if (Input.GetKey(KeyCode.LeftControl))
                s *= SLOW_WALK_SPEED_MOD;

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

    public bool cinematic_mode
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
                // Doesn't affect the obscurer/water
                if (r.transform.IsChildOf(obscurer.transform)) continue;
                if (r.transform.IsChildOf(water.transform)) continue;
                r.enabled = !value;
            }
        }
    }
    bool _cinematic_mode;

    public bool underwater
    {
        get => _underwater;
        private set
        {
            if (value == _underwater)
                return; // No change

            _underwater = value;

            if (!options_menu.global_volume.profile.TryGet(
                out UnityEngine.Rendering.HighDefinition.ColorAdjustments color))
                throw new System.Exception("No ColorAdjustments override on global volume!");

            if (!options_menu.global_volume.profile.TryGet(
                out UnityEngine.Rendering.HighDefinition.ChromaticAberration chroma))
                throw new System.Exception("No ChromaticAberration override on global volume!");

            if (!options_menu.global_volume.profile.TryGet(
                out UnityEngine.Rendering.HighDefinition.Vignette vignette))
                throw new System.Exception("No Vigentte override on global volume!");

            if (!options_menu.global_volume.profile.TryGet(
                out UnityEngine.Rendering.HighDefinition.DepthOfField dof))
                throw new System.Exception("No DepthOfField override on global volume!");

            dof.focusMode.value = value ?
                UnityEngine.Rendering.HighDefinition.DepthOfFieldMode.Manual :
                UnityEngine.Rendering.HighDefinition.DepthOfFieldMode.Off;

            color.colorFilter.value = value ? water.color : Color.white;
            chroma.intensity.value = value ? 1f : 0f;
            vignette.intensity.value = value ? 0.4f : 0f;


            if (value && bubbles == null)
            {
                bubbles = Resources.Load<ParticleSystem>("particle_systems/bubbles").inst();
                bubbles.transform.SetParent(transform);
                bubbles.transform.localPosition = Vector3.zero;
                bubbles.transform.localRotation = Quaternion.identity;
            }

            bubbles.gameObject.SetActive(value);
        }
    }
    bool _underwater;
    ParticleSystem bubbles;

    //#####################//
    // VIEW/CAMERA CONTROL //
    //#####################//

    void run_first_third_person()
    {
        if (map_open) return;

        if (Input.GetKeyDown(KeyCode.V))
            first_person = !first_person;
    }

    void run_mouse_look()
    {
        // Things that disallow camera movement
        if (Cursor.visible) return;
        if (!current_item_use_result.allows_look) return;

        if (cinematic_mode) mouse_look_fly();
        else mouse_look_normal();
    }

    void run_map()
    {
        // Things that don't allow interation with the map
        if (current_item_use != USE_TYPE.NOT_USING) return;

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

            // Scale camera clipping plane/shadow distance to work
            // with map-view render rangeas
            float eff_render_range = 
                (camera.transform.position - transform.position).magnitude + 
                game.render_range;
            camera.farClipPlane = eff_render_range * 1.5f;
            QualitySettings.shadowDistance = eff_render_range;
        }
    }

    void run_crosshairs()
    {
        crosshairs.enabled = !Cursor.visible;
    }

    void mouse_look_normal()
    {
        // Rotate the player view
        y_rotation.value += Input.GetAxis("Mouse X") * 5f;
        if (!map_open)
            x_rotation.value -= Input.GetAxis("Mouse Y") * 5f;
        else
        {
            // In map, so up/down rotation isn't networked    
            float eye_x = eye_transform.localRotation.eulerAngles.x;
            eye_x -= Input.GetAxis("Mouse Y") * 5f;
            eye_x = Mathf.Clamp(eye_x, 0, 90);
            eye_transform.localRotation = Quaternion.Euler(eye_x, 0, 0);
        }
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

    /// <summary> The player camera. </summary>
#if UNITY_EDITOR
    new
#endif
    public Camera camera
    { get; private set; }

    // Object used to obscure player view
    GameObject obscurer;

    public Color sky_color
    {
        get => utils.get_color(obscurer_renderer.material);
        set
        {
            camera.backgroundColor = value;
            utils.set_color(obscurer_renderer.material, value);
        }
    }
    Renderer obscurer_renderer;

    public bool obscurer_enabed
    {
        get => obscurer_renderer.enabled;
        set => obscurer_renderer.enabled = value;
    }

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

    void set_hand_position()
    {
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

        // Allign the arm with the hand
        right_arm.to_grab = equipped?.transform;
    }

    // Called when the render range changes
    public void update_render_range()
    {
        // Let the network know
        render_range = game.render_range;

        // Set the obscurer size to the render range
        obscurer.transform.localScale = Vector3.one * game.render_range * 0.99f;

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
            // Turn off the obscurer in map view
            obscurer.SetActive(!value);

            // Set the camera orthograpic if in 
            // map view, otherwise perspective
            camera.orthographic = value;

            if (value)
            {
                // Save camera rotation to restore later
                saved_camera_rotation = camera.transform.localRotation;

                // Eyes start in horizontal plane in map view
                x_rotation.value = 90;

                // Setup the camera in map mode/position   
                camera.orthographicSize = game.render_range;
                camera.transform.position = eye_transform.transform.position +
                    Vector3.up * (MAP_CAMERA_ALT - transform.position.y);
                camera.transform.rotation = Quaternion.LookRotation(
                    Vector3.down, transform.forward
                    );
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

    //########//
    // HEALTH //
    //########//

    public biome biome
    {
        get => _biome;
        private set
        {
            if (_biome == value)
                return; // No change
            _biome = value;
            enemies.biome = value;
        }
    }
    biome _biome;


    public biome.point point { get; private set; }

    public int max_health { get => 100; }

    float last_damaged_time = 0;

    void indicate_damage()
    {
        if (last_damaged_time == 0)
            return;

        if (!options_menu.global_volume.profile.TryGet(out UnityEngine.Rendering.HighDefinition.ColorAdjustments color))
            throw new System.Exception("No ColorAdjustments override on global volume!");

        float time_since_damaged = Time.realtimeSinceStartup - last_damaged_time;

        if (time_since_damaged < 1f)
            color.colorFilter.value = Color.Lerp(Color.red, Color.white, time_since_damaged);
        else
        {
            color.colorFilter.value = Color.white;
            last_damaged_time = 0;
        }
    }

    void heal_one_point()
    {
        heal(1);
    }

    public void take_damage(int damage)
    {
        health.value = Mathf.Max(0, health.value - damage);
        last_damaged_time = Time.realtimeSinceStartup;
    }

    public void heal(int amount)
    {
        health.value = Mathf.Min(max_health, health.value + amount);
    }

    //############//
    // NETWORKING //
    //############//

    networked_variables.net_int health;
    networked_variables.net_int slot_equipped;
    networked_variables.net_float y_rotation;
    networked_variables.net_float x_rotation;
    networked_variables.net_string username;

    public player_body body { get; private set; }
    public Transform eye_transform { get; private set; }
    arm right_arm;
    arm left_arm;
    water_reflections water;
    player_healthbar healthbar;

    public override void on_loose_authority()
    {
        throw new System.Exception("Authority should not be lost for players!");
    }

    public override void on_gain_authority()
    {
        if (camera != null)
            throw new System.Exception("Players should not gain authority more than once!");

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

        // The distance to the underwater screen, just past the near clipping plane
        float usd = camera.nearClipPlane * 1.1f;
        Vector3 bl_corner_point = camera.ScreenToWorldPoint(new Vector3(0, 0, usd));
        Vector3 tr_corner_point = camera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, usd));
        Vector3 delta = tr_corner_point - bl_corner_point;

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

        // Find the healthbar
        healthbar = FindObjectOfType<player_healthbar>();

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

        // Start passive healing
        InvokeRepeating("heal_one_point", 1f, 1f);

        // This is the local player
        current = this;
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

    public override void on_init_network_variables()
    {
        // Load the player body
        body = Resources.Load<player_body>("misc/player_body").inst();
        body.transform.SetParent(transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;

        // Scale the player body so the eyes are at the correct height
        eye_transform = body.eye_centre;
        float eye_y = (eye_transform.transform.position - transform.position).y;
        //var eye_centre = transform.position + Vector3.up * (HEIGHT - WIDTH / 2f) + transform.forward * 0.25f;
        body.transform.localScale *= (HEIGHT - WIDTH / 2f) / eye_y;

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

        // The players remaining health
        health = new networked_variables.net_int();
        health.on_change = () =>
        {
            healthbar?.set(health.value, max_health);
        };

        // The currently-equipped quickbar slot number
        slot_equipped = new networked_variables.net_int();
        slot_equipped.on_change = () =>
        {
            equipped = quickbar_slot(slot_equipped.value)?.item;
        };

        // y_rotation is the rotation of the player around the global y axis
        y_rotation = new networked_variables.net_float(resolution: 5f);
        y_rotation.on_change = () =>
        {
            transform.rotation = Quaternion.Euler(0, y_rotation.value, 0);
        };

        // x_rotation is the rotation of the eyes around their x axis and is clamped.
        x_rotation = new networked_variables.net_float(resolution: 5f, min_value: 0f, max_value: 160f);
        x_rotation.on_change = () =>
        {
            eye_transform.localRotation = Quaternion.Euler(x_rotation.value, 0, 0);
        };

        username = new networked_variables.net_string();
    }

    public override void on_first_create()
    {
        // Create the inventory object
        client.create(transform.position, "inventories/player_inventory", this);
        client.create(transform.position, "inventories/player_crafting_menu", this);
    }

    public override void on_add_networked_child(networked child)
    {
        // Connect to the inventory object
        if (child is inventory)
        {
            var inv = (inventory)child;
            var crafting_input = inv.ui.GetComponentInChildren<crafting_input>();

            if (crafting_input != null)
            {
                crafting_menu = inv;
                if (inventory == null)
                    throw new System.Exception("Inventory should be assigend before crafting menu!");

                crafting_input.craft_from = crafting_menu;
                crafting_input.craft_to = inventory;
            }
            else inventory = inv;
        }
    }

    //################//
    // STATIC METHODS //
    //################//

    public delegate void callback();
    private static callback waiting = () => { };

    // Schedule a function to be called when the current player becomes available
    public static void call_when_current_player_available(callback c)
    {
        if (current != null) c();
        else waiting += c;
    }

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
            waiting();
            waiting = () => { };
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

        var canv = FindObjectOfType<game>().main_canvas;

        m.transform = m.GetComponent<RectTransform>();
        m.transform.SetParent(canv.transform);
        m.transform.anchorMin = new Vector2(0.5f, 0.25f);
        m.transform.anchorMax = new Vector2(0.5f, 0.25f);
        m.transform.anchoredPosition = Vector2.zero;

        m.transform.anchoredPosition -=
            Vector2.up * 32f * canv.GetComponentsInChildren<popup_message>().Length;

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
    /// <summary> An inventory associated with this menu, that a 
    /// player can take/put items at will. </summary>
    inventory editable_inventory();

    /// <summary> The UI element associated with this menu (should create
    /// if it does not already exist). </summary>
    RectTransform left_menu_transform();

    /// <summary> Called when the player closes the left menu. </summary>
    void on_left_menu_close();

    /// <summary> Called when the player opens the left menu. </summary>
    void on_left_menu_open();
}
