using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player : networked_player, INotPathBlocking, IInspectable, ICanEquipArmour, IDontBlockItemLogisitcs, IAcceptsDamage
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
    public const float MAX_FLOAT_VELOCTY = 2f;
    public const float FALL_DAMAGE_START_SPEED = 10f;
    public const float FALL_DAMAGE_END_SPEED = 20f;

    // Movement
    public const float BASE_SPEED = 4f;
    public const float ACCELERATION_TIME = 0.1f;
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

    //####################//
    // STATIC CONSTRUCTOR //
    //####################//

    static player()
    {
        tips.add("You can change your hair color from the equipment " +
            "options menu (the cog in the top corner of the equipment" +
            " section of your inventory).");

        tips.add("Certain actions can only be performed with free hands. Press " +
            controls.current_bind(controls.BIND.QUICKBAR_1) +
            " a few times to de-equip what you are holding " +
            "(the cursor will change to a white circle).");

        tips.add("Your health will gradually regenerate as long as you are not too hungry.");

        tips.add("The green bar at the bottom of the screen is your health. " +
            "The orange bar at the bottom of the screen shows how hungry you are.");

        tips.add("You can switch between first and third-person views by pressing " +
            controls.current_bind(controls.BIND.TOGGLE_THIRD_PERSON) + ".");

        tips.add("Open the recipe book by pressing " +
            controls.current_bind(controls.BIND.OPEN_RECIPE_BOOK) + ".");

        tips.add("Look at a player and press " + controls.current_bind(controls.BIND.GIVE) +
            " to give them the item you currently have equipped.");
    }

    //##########//
    // UI STATE //
    //##########//

    public enum UI_STATE
    {
        ALL_CLOSED = 0,
        INVENTORY_OPEN,
        MAP_OPEN,
        RECIPE_BOOK_OPEN,
        OPTIONS_MENU_OPEN
    }

    /// <summary> Which UI windows are currently open 
    /// (excluding the inspection window, which can be 
    /// open alongside any UI) </summary>
    public UI_STATE ui_state
    {
        get => _ui_state;
        set
        {
            // If we're using an item, we can't change the UI state
            if (current_item_use.value != (int)USE_TYPE.NOT_USING) return;

            // Cinematic mode enforces ALL_CLOSED
            if (fly_mode) value = UI_STATE.ALL_CLOSED;

            // Close everything
            map_open = false;
            open_custom_menu = null;
            if (inventory != null) inventory.open = false;
            if (crafting_menu != null) crafting_menu.open = false;
            left_menu = null;
            options_menu.open = false;
            recipe.recipe_book.gameObject.SetActive(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            _ui_state = value;
            switch (_ui_state)
            {
                case UI_STATE.ALL_CLOSED:
                    break;

                case UI_STATE.INVENTORY_OPEN:
                    // Enable the cursor
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;

                    // Attempt to open a standalone custom menu
                    open_custom_menu = custom_menu_under_cursor;
                    if (open_custom_menu != null) break; // Custom menu open => don't do anything else

                    // No custom menu found, open the inventory
                    if (inventory != null) inventory.open = true;
                    if (crafting_menu != null) crafting_menu.open = true;

                    // Attempt also to open the left menu
                    left_menu = left_menu_under_cursor;
                    break;

                case UI_STATE.MAP_OPEN:
                    map_open = true;
                    break;

                case UI_STATE.RECIPE_BOOK_OPEN:

                    // Enable the cursor
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;

                    // Open the recipe book
                    recipe.recipe_book.gameObject.SetActive(true);
                    break;

                case UI_STATE.OPTIONS_MENU_OPEN:

                    // Enable the cursor
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;

                    // Open the options menu
                    options_menu.open = true;
                    break;

                default:
                    throw new System.Exception("Unkown UI_STATE!");
            }

            // We can see the crosshairs if we cant see the mouse
            crosshairs.enabled = !Cursor.visible;

            // Make sure the crafting options are up to date with whatever we're interacting with
            var crafting_options = crafting_menu?.ui?.GetComponentInChildren<player_crafting_options>(true);
            if (crafting_options != null) crafting_options.update_recipies();
        }
    }
    UI_STATE _ui_state;

    //####################//
    // INTERACTABLE STUFF //
    //####################//

    ILeftPlayerMenu left_menu_under_cursor
    {
        get
        {
            var ray = camera_ray(INTERACTION_RANGE, out float dist);
            return utils.raycast_for_closest<ILeftPlayerMenu>(ray, out RaycastHit hit, dist);
        }
    }

    ICustomMenu custom_menu_under_cursor
    {
        get
        {
            var ray = camera_ray(INTERACTION_RANGE, out float max_dis);
            return utils.raycast_for_closest<ICustomMenu>(ray, out RaycastHit hit, max_dis);
        }
    }

    IAcceptLeftClick left_clickable_under_cursor
    {
        get
        {
            var ray = camera_ray(INTERACTION_RANGE, out float dis);
            if (Physics.Raycast(ray, out RaycastHit hit, dis))
                return hit.collider.gameObject.GetComponentInParent<IAcceptLeftClick>();
            return null;
        }
    }

    IAcceptRightClick right_clickable_under_cursor
    {
        get
        {
            var ray = camera_ray(INTERACTION_RANGE, out float dis);
            if (Physics.Raycast(ray, out RaycastHit hit, dis))
                return hit.collider.gameObject.GetComponentInParent<IAcceptRightClick>();
            return null;
        }
    }

    player player_under_cursor
    {
        get
        {
            var ray = camera_ray(INTERACTION_RANGE, out float dis);
            return utils.raycast_for_closest<player>(ray, out RaycastHit hit, dis, (p) => p != this);
        }
    }

    item item_under_cursor
    {
        get
        {
            var ray = camera_ray(INTERACTION_RANGE, out float dist);
            return utils.raycast_for_closest<item>(ray, out RaycastHit hit, max_distance: dist);
        }
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    private void Update()
    {
        if (has_authority)
        {
            // Most things require authority to run
            run_context_tips();
            indicate_damage();
            run_world_generator();
            run_recipe_book();
            run_inspect_info();
            run_inventory();
            run_options();
            run_quickbar_shortcuts();
            run_map();
            run_first_third_person();
            run_mouse_look();
            run_movement();
            run_teleports();
        }
        else
        {
            // Non-authority only stuff (position lerping)
            // Lerp rotation
            transform.rotation = Quaternion.Euler(0, y_rotation.lerped_value, 0);
            eye_transform.rotation = Quaternion.Euler(x_rotation.lerped_value, y_rotation.lerped_value, 0);
        }

        // Stuff that runs both on authority/non-auth clients
        run_item_use();
        set_hand_position();
    }

    private void OnDrawGizmos()
    {
        if (controller != null)
        {
            Gizmos.color = controller.isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position + controller.radius * Vector3.up,
                                  controller.radius);
        }
    }

    void run_context_tips()
    {
        if (equipped != null)
        {
            var ect = equipped?.equipped_context_tip();
            if (ect != null)
            {
                tips.context_tip = ect;
                return;
            }
        }

        string context_tip = "";

        if (ui_state != UI_STATE.INVENTORY_OPEN)
        {
            var lm = left_menu_under_cursor;
            if (lm != null)
            {
                context_tip +=
                    "Press " + controls.current_bind(controls.BIND.OPEN_INVENTORY) +
                    " to interact with " + lm.left_menu_display_name() + "\n";
            }

            var cm = custom_menu_under_cursor;
            if (cm != null)
            {
                context_tip +=
                    "Press " + controls.current_bind(controls.BIND.OPEN_INVENTORY) +
                    " to interact\n";
            }
        }

        if (ui_state == UI_STATE.ALL_CLOSED)
        {
            if (equipped == null)
            {
                var lc = left_clickable_under_cursor;
                if (lc != null)
                {
                    var lct = lc.left_click_context_tip();
                    if (lct != null && lct.Length > 0)
                        context_tip += lct + "\n";
                }

                var rc = right_clickable_under_cursor;
                if (rc != null)
                {
                    var rct = rc.right_click_context_tip();
                    if (rct != null && rct.Length > 0)
                        context_tip += rct + "\n";
                }
            }
            else
            {
                var puc = player_under_cursor;
                if (puc != null)
                {
                    string to_give = equipped.display_name;
                    context_tip += "Press " + controls.current_bind(controls.BIND.GIVE) +
                        " to give " + puc.username.value + " " + utils.a_or_an(to_give) + " " + to_give + "\n";
                }
            }
        }

        var to_inspect = global::inspect_info.inspectable_under_cursor;
        if (to_inspect.inspecting != null)
            context_tip += "Press " + controls.current_bind(controls.BIND.INSPECT) + " to inspect\n";

        tips.context_tip = context_tip;
    }

    //###########//
    // INVENTORY //
    //###########//

    public inventory inventory { get; private set; }
    public inventory crafting_menu { get; private set; }

    void run_inventory()
    {
        // Toggle inventory
        if (controls.key_press(controls.BIND.OPEN_INVENTORY))
        {
            if (ui_state == UI_STATE.INVENTORY_OPEN) ui_state = UI_STATE.ALL_CLOSED;
            else ui_state = UI_STATE.INVENTORY_OPEN;
        }
    }

    void run_recipe_book()
    {
        // Toggle recipe book
        if (controls.key_press(controls.BIND.OPEN_RECIPE_BOOK))
        {
            if (ui_state == UI_STATE.RECIPE_BOOK_OPEN) ui_state = UI_STATE.ALL_CLOSED;
            else ui_state = UI_STATE.RECIPE_BOOK_OPEN;
        }
    }

    //#######//
    // MENUS //
    //#######//

    /// <summary> The menu that appears to the left of the inventory. </summary>
    public ILeftPlayerMenu left_menu
    {
        get => _left_menu;
        private set
        {
            if (_left_menu == value)
                return; // No change

            // Deactivate the old menu
            if (_left_menu != null)
            {
                _left_menu.on_left_menu_close();
                _left_menu.left_menu_transform().gameObject.SetActive(false);
            }

            // Activate the new menu
            _left_menu = value;
            if (_left_menu != null)
            {
                // Position the left menu at the left_expansion_point
                // but leave it parented to the canvas, rather than
                // the player inventory
                var rt = _left_menu.left_menu_transform();
                if (rt == null)
                    _left_menu = null;
                else
                {
                    rt.gameObject.SetActive(true);
                    var attach_point = inventory.ui.GetComponentInChildren<left_menu_attach_point>();
                    rt.SetParent(attach_point.transform);
                    rt.anchoredPosition = Vector2.zero;
                    rt.SetParent(FindObjectOfType<game>().main_canvas.transform);
                    _left_menu.on_left_menu_open();
                }
            }
        }
    }
    ILeftPlayerMenu _left_menu;

    ICustomMenu open_custom_menu
    {
        get => _open_custom_menu;
        set
        {
            if (_open_custom_menu == value)
                return; // No change

            // Close previous menu
            if (_open_custom_menu != null)
                _open_custom_menu.close_custom_menu();

            // Open new menu
            _open_custom_menu = value;
            if (_open_custom_menu != null)
                _open_custom_menu.open_custom_menu();
        }
    }
    ICustomMenu _open_custom_menu;

    //#################//
    // ICanEquipArmour //
    //#################//

    public armour_locator[] armour_locators() { return GetComponentsInChildren<armour_locator>(); }
    public float armour_scale() { return 1f; }
    public Color hair_color() { return net_hair_color.value; }

    //##########//
    // ITEM USE //
    //##########//

    // Called on a left click when no item is equipped
    public void left_click_with_hand() { left_clickable_under_cursor?.on_left_click(); }

    // Called on a right click when no item is equipped
    public void right_click_with_hand() { right_clickable_under_cursor?.on_right_click(); }

    // The ways that we can use an item
    public enum USE_TYPE
    {
        NOT_USING,
        USING_LEFT_CLICK,
        USING_RIGHT_CLICK,
    }

    void give_to_player()
    {
        if (equipped == null) return;
        string to_give = equipped.name;
        string to_give_display_name = equipped.display_name;

        var giving_to = player_under_cursor;
        if (giving_to == null) return;

        if (inventory.remove(to_give, 1))
        {
            popup_message.create("Gave " + to_give_display_name +
                                 " to " + giving_to.username.value);
            giving_to.inventory.add(to_give, 1);
        }
    }

    item.use_result current_item_use_result;
    void run_item_use()
    {
        // Don't allow item use when in UI, or when flying
        if (ui_state != UI_STATE.ALL_CLOSED) return;
        if (fly_mode) return;

        // Use items
        current_item_use_result = item.use_result.complete;
        if (current_item_use.value == (int)USE_TYPE.NOT_USING)
        {
            // Only authority clients can start item use
            if (has_authority)
            {
                bool left_click = controls.mouse_click(controls.MOUSE_BUTTON.LEFT) ||
                    (equipped == null ? false :
                        equipped.allow_left_click_held_down() &&
                        controls.mouse_down(controls.MOUSE_BUTTON.LEFT));

                bool right_click = controls.mouse_click(controls.MOUSE_BUTTON.RIGHT) ||
                    (equipped == null ? false :
                        equipped.allow_right_click_held_down() &&
                        controls.mouse_down(controls.MOUSE_BUTTON.RIGHT));

                // Start a new use type
                if (left_click)
                {
                    if (equipped == null) left_click_with_hand();
                    else current_item_use.value = (int)USE_TYPE.USING_LEFT_CLICK;
                }
                else if (right_click)
                {
                    if (equipped == null) right_click_with_hand();
                    else current_item_use.value = (int)USE_TYPE.USING_RIGHT_CLICK;
                }
                else if (controls.key_press(controls.BIND.GIVE))
                    give_to_player();
            }
        }
        else
        {
            // Continue item use (both on auth client and non-auth client)
            if (equipped == null) current_item_use_result = item.use_result.complete;
            else current_item_use_result = equipped.on_use_continue(
                (USE_TYPE)current_item_use.value, this);

            // If use has completed, then stop using (on authority client)
            if (!current_item_use_result.underway && has_authority)
                current_item_use.value = (int)USE_TYPE.NOT_USING;
        }

        // Run undo/redo commands
        if (current_item_use.value == (int)USE_TYPE.NOT_USING && has_authority)
        {
            if (controls.key_press(controls.BIND.UNDO)) undo_manager.undo();
            if (controls.key_press(controls.BIND.REDO)) undo_manager.redo();
        }
    }

    //########################//
    // EQUIP + QUICKBAR SLOTS //
    //########################//

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
            {
                // Call implementation-specific on_unequip
                // stuff + destroy the item
                _equipped.on_unequip(has_authority);
                Destroy(_equipped.gameObject);
            }

            if (value == null)
            {
                _equipped = null;
            }
            else
            {
                // Ensure we actually have one of these in my inventory
                if (inventory.contains(value))
                    _equipped = item.create(value.name, transform.position, transform.rotation);
                else _equipped = null; // Don't have, equip null
            }

            // If this is the local player, set the cursor (do this before we call
            // on_equip, so on_equip can override the cursor if it wants)
            if (has_authority)
                cursor_sprite = _equipped == null ? cursors.DEFAULT : _equipped.sprite.name;

            if (_equipped != null)
            {
                // Parent the equipped object to the hand
                _equipped.transform.SetParent(hand_centre);
                _equipped.transform.localPosition = Vector3.zero;
                _equipped.transform.localRotation = Quaternion.identity;

                // Call implementation-specific on_equip stuff
                _equipped.on_equip(has_authority);
            }
        }
    }

    public inventory_slot_networked quickbar_slot(int n)
    {
        // Move to zero-offset array
        return inventory?.nth_slot(n - 1);
    }

    public void validate_equip()
    {
        var slot = quickbar_slot(slot_equipped.value);

        if (slot == null || slot.count == 0 || slot.item == null)
        {
            // Nothing in slot, unequip
            slot_equipped.value = 0;
        }
        else
        {
            // Re-equip whatever is in the slot
            int tmp = slot_equipped.value;
            slot_equipped.value = 0;
            slot_equipped.value = tmp;
        }
    }

    void toggle_equip(int slot)
    {
        // Toggle equipping the item in the given slot
        if (slot_equipped.value == slot) slot_equipped.value = 0;
        else slot_equipped.value = slot;
    }

    void run_quickbar_shortcuts()
    {
        // Can't use quickbar shortcuts from the UI, or if we're 
        // using an item, or if we're flying
        if (ui_state != UI_STATE.ALL_CLOSED) return;
        if (current_item_use.value != (int)USE_TYPE.NOT_USING) return;
        if (fly_mode) return;

        const int QUICKBAR_SLOTS_COUNT = 8;

        // Select something in the world/equip it if we have one
        if (controls.key_press(controls.BIND.SELECT_ITEM_FROM_WORLD))
        {
            var itm = item_under_cursor;
            if (itm != null)
            {
                // If we've already got the item in the quickbar, just equip it
                for (int i = 1; i <= QUICKBAR_SLOTS_COUNT; ++i)
                    if (quickbar_slot(i)?.item?.name == itm.name)
                    {
                        slot_equipped.value = i;
                        return;
                    }

                var slot_found = inventory.find_slot_by_item(itm);
                if (slot_found != null)
                {
                    // We've found the item in our inventory
                    // Switch with the currently equipped slot
                    // (or the first slot if nothing is equipped)             
                    int swith_with_slot = Mathf.Max(slot_equipped.value, 1);
                    var switch_with = quickbar_slot(swith_with_slot);

                    if (switch_with == null)
                    {
                        // Inventory slot doesn't exist, create one
                        switch_with = (inventory_slot_networked)client.create(
                            transform.position, "misc/networked_inventory_slot", inventory);
                        switch_with.set_item_count_index(null, 0, swith_with_slot - 1);
                    }

                    slot_found.switch_with(switch_with);
                    slot_equipped.value = swith_with_slot;
                    validate_equip();
                }
            }
        }

        // Select quickbar item using keyboard shortcut
        if (controls.key_press(controls.BIND.QUICKBAR_1)) toggle_equip(1);
        else if (controls.key_press(controls.BIND.QUICKBAR_2)) toggle_equip(2);
        else if (controls.key_press(controls.BIND.QUICKBAR_3)) toggle_equip(3);
        else if (controls.key_press(controls.BIND.QUICKBAR_4)) toggle_equip(4);
        else if (controls.key_press(controls.BIND.QUICKBAR_5)) toggle_equip(5);
        else if (controls.key_press(controls.BIND.QUICKBAR_6)) toggle_equip(6);
        else if (controls.key_press(controls.BIND.QUICKBAR_7)) toggle_equip(7);
        else if (controls.key_press(controls.BIND.QUICKBAR_8)) toggle_equip(8);

        // Scroll through quickbar items
        float sw = controls.get_axis("Mouse ScrollWheel");

        if (sw != 0)
        {
            int new_slot = slot_equipped.value;
            for (int attempt = 0; attempt < QUICKBAR_SLOTS_COUNT; ++attempt)
            {
                if (sw > 0) ++new_slot;
                else if (sw < 0) --new_slot;

                if (new_slot < 0) new_slot = QUICKBAR_SLOTS_COUNT;
                if (new_slot > QUICKBAR_SLOTS_COUNT) new_slot = 0;

                // Stop if we've hit a slot with an item, or if we've scrolled
                // off the end (in which case we will equip nothing)
                if (quickbar_slot(new_slot)?.item != null || new_slot == 0)
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
        // Note that the inspection window can be 
        // opened independently of the UI state
        inspection_ui.visible = controls.key_down(controls.BIND.INSPECT);
    }

    inspect_info inspection_ui
    {
        get
        {
            // Create the inspect_info object if it doesn't already exist
            if (_inspection_ui == null)
            {
                _inspection_ui = Resources.Load<inspect_info>("ui/inspect_info").inst();
                _inspection_ui.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
                _inspection_ui.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            }

            return _inspection_ui;
        }
    }
    inspect_info _inspection_ui;

    //###########//
    //  MOVEMENT //
    //###########//

    CharacterController controller;
    Vector3 velocity = Vector3.zero;

    void run_movement()
    {
        // Things that disallow movement
        if (ui_state == UI_STATE.OPTIONS_MENU_OPEN) return;
        if (left_menu != null) return;
        if (!current_item_use_result.allows_move) return;

        move();
        float_in_water();
    }

    void run_teleports()
    {
        // Carry out home teleports
        if (controls.key_press(controls.BIND.HOME_TELEPORT))
        {
            var tm = FindObjectOfType<teleport_manager>();
            if (tm != null)
                teleport(tm.nearest_teleport_destination(transform.position));
        }
    }

    bool can_jump()
    {
        foreach (var c in Physics.BoxCastAll(transform.position + Vector3.up,
            Vector3.one * WIDTH / 2f, Vector3.down, transform.rotation, 1.5f))
            if (!c.transform.IsChildOf(transform))
                return true;
        return false;
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

        if (fly_mode) fly_move();
        else normal_move();

        // Update the network with our new position
        networked_position = transform.position;
    }

    void float_in_water()
    {
        // We're underwater if the bottom of the screen is underwater
        var ray = camera.ScreenPointToRay(new Vector3(Screen.width / 2f, 0, 0));
        float dis = camera.nearClipPlane / Vector3.Dot(ray.direction, -camera.transform.up);
        float eff_eye_y = (ray.origin + ray.direction * dis).y;
        underwater = eff_eye_y < world.SEA_LEVEL && !map_open && eff_eye_y > world.UNDERGROUND_ROOF;

        // Don't float underground
        if (eff_eye_y < world.UNDERGROUND_ROOF) return;

        float amt_submerged = (world.SEA_LEVEL - transform.position.y) / HEIGHT;
        if (amt_submerged <= 0) return;
        if (amt_submerged > 1.0f) amt_submerged = 1.0f;

        // Bouyancy (sink if shift is held, don't allow buildup of too much y velocity)
        if (!controls.key_down(controls.BIND.SINK) && velocity.y < MAX_FLOAT_VELOCTY)
            velocity.y += amt_submerged * (GRAVITY + BOUYANCY) * Time.deltaTime;

        // Drag
        velocity -= velocity * amt_submerged * WATER_DRAG * Time.deltaTime;
    }

    void normal_move()
    {
        Vector3 move = Vector3.zero;

        // Climb ladders
        bool climbing_ladder = false;
        if (controls.key_down(controls.BIND.WALK_FORWARD) ||
            controls.key_down(controls.BIND.PAUSE_ON_LADDER))
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
                    if (controls.key_down(controls.BIND.PAUSE_ON_LADDER))
                        velocity.y = 0;
                }
            }

        // Turn on/off crouch
        if (ui_state != UI_STATE.ALL_CLOSED || climbing_ladder)
            crouched.value = false;
        else
            crouched.value = controls.key_down(controls.BIND.CROUCH);

        // Jumping
        if (controls.key_press(controls.BIND.JUMP) && can_jump())
            velocity.y = JUMP_VEL;

        if (controller.isGrounded)
        {
            if (velocity.y < -1f)
            {
                // Fall damage
                if (velocity.y < -FALL_DAMAGE_START_SPEED)
                {
                    if (disable_next_fall_damage)
                    {
                        disable_next_fall_damage = false;
                    }
                    else
                    {
                        float fd = -velocity.y;
                        fd -= FALL_DAMAGE_START_SPEED;
                        fd /= (FALL_DAMAGE_END_SPEED - FALL_DAMAGE_START_SPEED);
                        take_damage((int)(fd * 100));
                    }
                }

                // Ensure we don't accumulate too much -ve y velocity
                velocity.y = -1f;
            }
        }
        else // Not grounded
        {
            // Gravity
            if (!climbing_ladder) velocity.y -= GRAVITY * Time.deltaTime;
        }

        // Control forward/back velocity
        if (controls.key_down(controls.BIND.WALK_FORWARD))
            velocity += transform.forward * ACCELERATION * Time.deltaTime;
        else if (controls.key_down(controls.BIND.WALK_BACKWARD))
            velocity -= transform.forward * ACCELERATION * Time.deltaTime;
        else velocity -= Vector3.Project(velocity, transform.forward);

        // Control left/right veloctiy
        if (controls.key_down(controls.BIND.STRAFE_RIGHT))
            velocity += camera.transform.right * ACCELERATION * Time.deltaTime;
        else if (controls.key_down(controls.BIND.STRAFE_LEFT))
            velocity -= camera.transform.right * ACCELERATION * Time.deltaTime;
        else velocity -= Vector3.Project(velocity, camera.transform.right);

        move += velocity * Time.deltaTime;

        // Ensure speed in x-z plane does not exceed movement speed
        float xz = new Vector3(velocity.x, 0, velocity.z).magnitude;
        if (xz > speed)
        {
            velocity.x *= speed / xz;
            velocity.z *= speed / xz;
        }

        // In water, allow climbing out
        if (transform.position.y < world.SEA_LEVEL &&
            transform.position.y > world.UNDERGROUND_ROOF &&
            controls.key_down(controls.BIND.WALK_FORWARD))
        {
            // Look for solid objects in front of player
            if (velocity.y < 2f)
                foreach (var h in Physics.CapsuleCastAll(
                    transform.position + Vector3.up * controller.radius,
                    transform.position + Vector3.up * (controller.height - controller.radius),
                    controller.radius, transform.forward, controller.radius))
                {
                    if (h.transform.IsChildOf(transform)) continue;
                    velocity.y += ACCELERATION * Time.deltaTime;
                    break;
                }
        }

        controller.Move(move);

        // Ensure we stay above the terrain (unless we're underground)
        if (transform.position.y > world.UNDERGROUND_ROOF)
        {
            Vector3 pos = transform.position;
            pos.y = world.MAX_ALTITUDE;
            RaycastHit terra_hit;
            var tc = utils.raycast_for_closest<TerrainCollider>(new Ray(pos, Vector3.down), out terra_hit);
            if (terra_hit.point.y > transform.position.y)
                transform.position = terra_hit.point;
        }
    }

    const float FLY_SPEED_RESET = 10f;
    const float FLY_ACCELERATION = 10f;
    float fly_speed = FLY_SPEED_RESET;

    void fly_move()
    {
        crouched.value = false;
        Vector3 fw = map_open ? transform.forward : camera.transform.forward;
        Vector3 ri = camera.transform.right;
        Vector3 move = Vector3.zero;

        if (controls.key_down(controls.BIND.WALK_FORWARD)) move += fw * fly_speed * Time.deltaTime;
        if (controls.key_down(controls.BIND.WALK_BACKWARD)) move -= fw * fly_speed * Time.deltaTime;
        if (controls.key_down(controls.BIND.STRAFE_RIGHT)) move += ri * fly_speed * Time.deltaTime;
        if (controls.key_down(controls.BIND.STRAFE_LEFT)) move -= ri * fly_speed * Time.deltaTime;
        if (controls.key_down(controls.BIND.FLY_UP)) move += Vector3.up * fly_speed * Time.deltaTime;
        if (controls.key_down(controls.BIND.FLY_DOWN)) move -= Vector3.up * fly_speed * Time.deltaTime;

        if (move.magnitude > 10e-4f)
            fly_speed += Time.deltaTime * FLY_ACCELERATION;
        else
            fly_speed = FLY_SPEED_RESET;


        controller.Move(move);

        if (controls.key_press(controls.BIND.ADD_CINEMATIC_KEYFRAME) ||
            controls.mouse_click(controls.MOUSE_BUTTON.LEFT))
            cinematic_recording.add_keyframe(camera.transform.position, camera.transform.rotation);

        if (controls.key_press(controls.BIND.REMOVE_LAST_CINEMATIC_KEYFRAME) ||
            controls.mouse_click(controls.MOUSE_BUTTON.RIGHT))
            cinematic_recording.remove_last_keyframe();

        if (controls.key_press(controls.BIND.TOGGLE_CINEMATIC_PLAYBACK))
            cinematic_recording.toggle_playback();
    }

    public void teleport(Vector3 location)
    {
        if (controller == null)
            return;

        // If we've teleported reasonably far, reduce render range 
        // to reduce lagging out.
        if ((location - networked_position).magnitude > game.render_range / 2f)
        {
            game.render_range = Mathf.Min(game.render_range, game.DEFAULT_RENDER_RANGE);
            game.render_range_target = game.render_range;
        }

        ui_state = UI_STATE.ALL_CLOSED;
        controller.enabled = false;
        networked_position = location;

        // Re-enable the controller once the chunk at the new location is generated
        var chunk_coords = chunk.coords(location);
        chunk.add_generation_listener(transform, chunk_coords[0], chunk_coords[1], (c) =>
        {
            controller.enabled = true;
        });
    }

    public float speed
    {
        get
        {
            float s = BASE_SPEED;
            if (crouched.value)
                s *= CROUCH_SPEED_MOD;
            else if (controls.key_down(controls.BIND.SLOW_WALK))
                s *= SLOW_WALK_SPEED_MOD;

            return s;
        }
    }

    bool disable_next_fall_damage = false;
    public bool fly_mode
    {
        get => _fly_mode;
        set
        {
            _fly_mode = value;

            ui_state = UI_STATE.ALL_CLOSED;
            cursor_sprite = _fly_mode ? null : cursors.DEFAULT;

            // Disable (or re-enable) ui things
            healthbar.gameObject.SetActive(!_fly_mode);
            foodbar.gameObject.SetActive(!_fly_mode);
            toolbar_display_slot.toolbar_active = !_fly_mode;
            compass.active = !_fly_mode;
            if (!_fly_mode)
            {
                cinematic_recording.stop_playback();
                disable_next_fall_damage = true;
            }

            // Make the player (in)visible
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                // Doesn't affect the sky/water
                if (r.transform.IsChildOf(physical_sky.transform)) continue;
                if (r.transform.IsChildOf(water.transform)) continue;
                r.enabled = !_fly_mode;
            }
        }
    }
    bool _fly_mode;

    public bool underwater
    {
        get => _underwater;
        private set
        {
            if (value == _underwater)
                return; // No change

            _underwater = value;

            // Get the various postprocessing things we need
            // to set to make the underwater effect.
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

            // Create/remove the underwater effect
            dof.focusMode.value = _underwater ?
                UnityEngine.Rendering.HighDefinition.DepthOfFieldMode.Manual :
                UnityEngine.Rendering.HighDefinition.DepthOfFieldMode.Off;

            color.colorFilter.value = _underwater ? water.color : Color.white;
            chroma.intensity.value = _underwater ? 1f : 0f;
            vignette.intensity.value = _underwater ? 0.4f : 0f;

            if (_underwater && bubbles == null)
            {
                // Create the bubbles if they don't already exist
                bubbles = Resources.Load<ParticleSystem>("particle_systems/bubbles").inst();
                bubbles.transform.SetParent(transform);
                bubbles.transform.localPosition = Vector3.zero;
                bubbles.transform.localRotation = Quaternion.identity;
            }

            bubbles.gameObject.SetActive(_underwater);
        }
    }
    bool _underwater;
    ParticleSystem bubbles;

    //#####################//
    // VIEW/CAMERA CONTROL //
    //#####################//

    void run_first_third_person()
    {
        // Only allow toggle first/third when no UI is open
        if (ui_state != UI_STATE.ALL_CLOSED) return;

        if (controls.key_press(controls.BIND.TOGGLE_THIRD_PERSON))
            first_person = !first_person;
    }

    void run_mouse_look()
    {
        // Things that disallow camera movement
        if (!(ui_state == UI_STATE.ALL_CLOSED ||
              ui_state == UI_STATE.MAP_OPEN)) return;
        if (!current_item_use_result.allows_look) return;

        // Ping the map
        if (controls.mouse_click(controls.MOUSE_BUTTON.MIDDLE))
        {
            var ray = camera_ray();
            if (Physics.Raycast(ray, out RaycastHit hit))
                client.create(hit.point, "misc/map_ping");
        }

        mouse_look();
    }

    void run_map()
    {
        // Things that don't allow interation with the map
        if (current_item_use.value != (int)USE_TYPE.NOT_USING) return;

        // Toggle the map view on M
        if (controls.key_press(controls.BIND.TOGGLE_MAP))
        {
            if (ui_state == UI_STATE.MAP_OPEN) ui_state = UI_STATE.ALL_CLOSED;
            else ui_state = UI_STATE.MAP_OPEN;
        }

        if (map_open)
        {
            // Zoom the map
            float scroll = controls.get_axis("Mouse ScrollWheel");
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

    void run_options()
    {
        // Toggle options
        if (controls.key_press(controls.BIND.TOGGLE_OPTIONS))
        {
            if (ui_state == UI_STATE.OPTIONS_MENU_OPEN) ui_state = UI_STATE.ALL_CLOSED;
            else ui_state = UI_STATE.OPTIONS_MENU_OPEN;
        }
    }

    // The current cursor sprite
    public string cursor_sprite
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
            if (cursor_sprite == value) return;
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
    UnityEngine.UI.Image crosshairs;

    void mouse_look()
    {
        // Rotate the player view
        y_rotation.value += controls.get_axis("Mouse X") * controls.mouse_look_sensitivity;
        if (!map_open)
            x_rotation.value -= controls.get_axis("Mouse Y") * controls.mouse_look_sensitivity;
        else
        {
            // In map, so up/down rotation isn't networked    
            float eye_x = eye_transform.localRotation.eulerAngles.x;
            eye_x -= controls.get_axis("Mouse Y") * controls.mouse_look_sensitivity;
            eye_x = Mathf.Clamp(eye_x, 0, 90);
            eye_transform.localRotation = Quaternion.Euler(eye_x, 0, 0);
        }
    }

    public void set_look_rotation(Quaternion rotation)
    {
        x_rotation.value = utils.minimal_modulus_angle(rotation.eulerAngles.x);
        y_rotation.value = utils.minimal_modulus_angle(rotation.eulerAngles.y);
    }

    /// <summary> The player camera. </summary>
#if UNITY_EDITOR
    new
#endif
    public Camera camera
    { get; private set; }

    /// <summary> Returns true if the given location is within the current viewport. </summary>
    public bool in_field_of_view(Vector3 position)
    {
        // If it's within 5m of the camera, consider it in the FOV
        if ((position - camera.transform.position).magnitude < 5f)
            return true;

        var vp = camera.WorldToViewportPoint(position);

        // Below bottom-left of screen
        if (vp.x < 0) return false;
        if (vp.y < 0) return false;

        // Above top-right of screen
        if (vp.x > 1) return false;
        if (vp.y > 1) return false;

        // Behind camera
        if (vp.z < 0) return false;

        return true;
    }

    /// <summary> The in-game sky sphere. </summary>
    Renderer physical_sky;
    public bool physical_sky_enabled
    {
        get => physical_sky.enabled;
        set => physical_sky.enabled = value;
    }

    public Color sky_color
    {
        get => utils.get_color(physical_sky.material);
        set
        {
            var hd_cam = camera.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            if (hd_cam != null)
            {
                hd_cam.backgroundColorHDR = value;
            }
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = value;
            utils.set_color(physical_sky.material, value);
        }
    }

    bool head_visible
    {
        set
        {
            var head = GetComponentInChildren<body>().head;
            foreach (var r in head.GetComponentsInChildren<Renderer>(true))
            {
                // Don't make eqipped things invisible
                if (equipped != null)
                    if (r.transform.IsChildOf(equipped?.transform))
                        continue;

                // Make invisible, but keep shadows
                r.shadowCastingMode = value ?
                    UnityEngine.Rendering.ShadowCastingMode.On :
                    UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }
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
            head_visible = !value;

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
        if (equipped == null) left_arm.to_grab = null;
    }

    // Called when the render range changes
    public void update_render_range()
    {
        // Let the network know
        render_range = game.render_range;

        // Set the sky size to the render range
        physical_sky.transform.localScale = Vector3.one * game.render_range * 0.99f;

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
            // Turn off the physical sky in map view
            physical_sky_enabled = !value && options_menu.get_bool("physical_sky");

            // Set the camera orthograpic if in 
            // map view, otherwise perspective
            camera.orthographic = value;

            if (value)
            {
                // Save camera rotation to restore later
                saved_camera_rotation = camera.transform.localRotation;

                // Eyes start in horizontal plane in map view
                x_rotation.value = 0;

                // Setup the camera in map mode/position   
                camera.orthographicSize = game.render_range;
                camera.transform.position = eye_transform.transform.position + Vector3.up * MAP_CAMERA_ALT;
                camera.transform.rotation = Quaternion.LookRotation(Vector3.down, transform.forward);
            }
            else
            {
                // Restore 3D camera view
                camera.transform.localRotation = saved_camera_rotation;
                first_person = first_person; // This is needed
                x_rotation.on_change();
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

    //#####//
    // MAP //
    //#####//

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

    public biome biome
    {
        get => _biome;
        private set
        {
            if (_biome == value)
                return; // No change
            _biome = value;
        }
    }
    biome _biome;

    public biome.point point { get; private set; }

    //########//
    // HEALTH //
    //########//

    public bool is_dead = false;

    float last_damaged_time = 0;

    void indicate_damage()
    {
        if (last_damaged_time == 0 || is_dead)
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

    int passive_effect_counter = 0;
    void passive_effect_update()
    {
        if (is_dead || fly_mode) return;

        ++passive_effect_counter;
        if (hunger.value > 50) heal(1);
        else if (hunger.value == 0) take_damage(1);

        if (passive_effect_counter % 20 == 0)
            modify_hunger(-1);
    }

    public void take_damage(int damage)
    {
        if (is_dead) return;
        health.value = Mathf.Max(0, health.value - damage);
    }

    public void heal(int amount)
    {
        if (is_dead) return;
        health.value = Mathf.Min(100, health.value + amount);
    }

    public void modify_hunger(int amount)
    {
        hunger.value = Mathf.Clamp(hunger.value + amount, 0, 100);
    }

    void die()
    {
        // Create the sack containing my inventory
        var inv_contents = inventory.contents();
        inventory.clear();
        sack.create(transform.position, inv_contents, username.value + "'s remains");

        // Create the respawn timer
        var timer = Resources.Load<respawn_timer>("ui/respawn_timer").inst();
        timer.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
        timer.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
        timer.to_respawn = this;
        is_dead = true;

        // Add the red tint
        if (!options_menu.global_volume.profile.TryGet(out UnityEngine.Rendering.HighDefinition.ColorAdjustments color))
            throw new System.Exception("No ColorAdjustments override on global volume!");
        color.colorFilter.value = Color.red;
    }

    public void respawn()
    {
        teleport(respawn_point.value);
        is_dead = false;

        heal(100);

        if (!options_menu.global_volume.profile.TryGet(out UnityEngine.Rendering.HighDefinition.ColorAdjustments color))
            throw new System.Exception("No ColorAdjustments override on global volume!");
        color.colorFilter.value = Color.white;
    }

    //##############//
    // IINspectable //
    //##############//

    public string inspect_info() { return username.value; }
    public Sprite main_sprite() { return null; }
    public Sprite secondary_sprite() { return null; }

    //############//
    // NETWORKING //
    //############//

    networked_variables.net_int health;
    networked_variables.net_int hunger;
    networked_variables.net_int current_item_use;
    networked_variables.net_int slot_equipped;
    networked_variables.net_float y_rotation;
    networked_variables.net_float x_rotation;
    networked_variables.net_string username;
    networked_variables.net_bool crouched;
    networked_variables.net_vector3 respawn_point;
    networked_variables.net_color net_hair_color;

    public int slot_number_equipped => slot_equipped.value;

    public player_body body { get; private set; }
    public Transform eye_transform { get; private set; }
    AudioSource sound_source;
    arm right_arm;
    arm left_arm;
    water_reflections water;
    player_healthbar healthbar;
    player_healthbar foodbar;

    public void mod_x_rotation(float mod) { x_rotation.value += mod; }
    public void mod_y_rotation(float mod) { y_rotation.value += mod; }

    public void play_sound(string sound,
        float min_pitch = 1f, float max_pitch = 1f, float volume = 1f)
    {
        sound_source.Stop();
        sound_source.pitch = Random.Range(min_pitch, max_pitch);
        sound_source.PlayOneShot(Resources.Load<AudioClip>(sound), volume);
    }

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
        camera.nearClipPlane = 0.01f;
        first_person = true; // Start with camera in 1st person position

        // Enforce the render limit with a sky-color object
        var sky = Resources.Load<GameObject>("misc/physical_sky").inst();
        sky.transform.SetParent(transform);
        sky.transform.localPosition = Vector3.zero;
        physical_sky = sky.GetComponentInChildren<Renderer>();

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
        cursor_sprite = "default_cursor";

        // Find the healthbars
        foreach (var hb in FindObjectsOfType<player_healthbar>())
        {
            switch (hb.type)
            {
                case player_healthbar.TYPE.HEALTH:
                    healthbar = hb;
                    healthbar.set(health.value, 100);
                    break;

                case player_healthbar.TYPE.FOOD:
                    foodbar = hb;
                    foodbar.set(hunger.value, 100);
                    break;

                default:
                    throw new System.Exception("Unkown healthbar type!");
            }
        }

        // Create the water
        water = new GameObject("water").AddComponent<water_reflections>();

        // Ensure sky color is set properly
        sky_color = sky_color;

        // Initialize the render range
        update_render_range();

        // Start with the map closed, first person view
        map_open = false;

        // Start looking to add the player controller
        Invoke("add_controller", 0.1f);

        // Set the passive effect tick going
        InvokeRepeating("passive_effect_update", 1f, 1f);

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

        // Create my sound source
        sound_source = new GameObject("sound_source").AddComponent<AudioSource>();
        sound_source.transform.SetParent(transform);
        sound_source.transform.localPosition = new Vector3(0, HEIGHT - WIDTH / 2f, WIDTH / 2f);
        sound_source.spatialBlend = 0f;

        // Network my username
        username = new networked_variables.net_string();

        // The place where the player respawns
        respawn_point = new networked_variables.net_vector3();

        // The players remaining health
        health = new networked_variables.net_int(default_value: 100);
        health.on_change = () =>
        {
            healthbar?.set(health.value, 100);

            if (health.value <= 0)
            {
                if (this == current) die();
                popup_message.create(username.value + " has died!");
            }
        };

        // Check for decreases in health (i.e. damage)
        health.on_change_old_new = (old_health, new_health) =>
        {
            if (new_health < old_health)
                last_damaged_time = Time.realtimeSinceStartup;
        };

        // How fed the player is
        hunger = new networked_variables.net_int(default_value: 100);
        hunger.on_change = () =>
        {
            foodbar?.set(hunger.value, 100);
            if (hunger.value <= 0 && this == current)
                popup_message.create("You are starving!");
        };

        // The currently-equipped quickbar slot number
        slot_equipped = new networked_variables.net_int();
        slot_equipped.on_change = () =>
        {
            equipped = quickbar_slot(slot_equipped.value)?.item;
            if (this == current)
                toolbar_display_slot.update_selected(slot_equipped.value);
        };

        // y_rotation is the rotation of the player around the global y axis
        y_rotation = new networked_variables.net_float(resolution: 5f);
        y_rotation.on_change = () =>
        {
            if (has_authority)
                transform.rotation = Quaternion.Euler(0, y_rotation.value, 0);
        };

        // x_rotation is the rotation of the eyes around their x axis and is clamped.
        x_rotation = new networked_variables.net_float(resolution: 5f, min_value: -90f, max_value: 90f);
        x_rotation.on_change = () =>
        {
            if (has_authority)
                eye_transform.rotation = Quaternion.Euler(x_rotation.value, y_rotation.value, 0);
        };

        // Network the players crouch state
        crouched = new networked_variables.net_bool();
        crouched.on_change = () =>
        {
            // Can't crouch in fly mode
            if (fly_mode && crouched.value) crouched.value = false;

            if (crouched.value) body.transform.localPosition = new Vector3(0, -0.25f, 0);
            else body.transform.localPosition = Vector3.zero;
        };

        // Network the player hair color
        net_hair_color = new networked_variables.net_color();
        net_hair_color.on_change = () =>
        {
            // Refresh hair color
            foreach (var al in armour_locators())
                if (al.equipped != null && al.equipped is hairstyle)
                    al.equipped.on_equip(this);
        };

        // Are we currently using the item, if so, how?
        current_item_use = new networked_variables.net_int(default_value: (int)USE_TYPE.NOT_USING);
        current_item_use.on_change_old_new = (old_val, new_val) =>
        {
            if (new_val == (int)USE_TYPE.NOT_USING)
            {
                // Stopped using
                equipped?.on_use_end((USE_TYPE)old_val, this);
            }
            else // Started using
            {
                if (equipped == null)
                {
                    // Nothing equipped. If we're on the auth 
                    // client, immediately stop using.
                    if (has_authority)
                        current_item_use.value = (int)USE_TYPE.NOT_USING;
                    return;
                }

                // Start using item
                if (!equipped.on_use_start((USE_TYPE)new_val, this).underway)
                {
                    // Immediately completed. If we're on the auth
                    // client, stop using - this might mean that
                    // on_use_start is not called on remote clients
                    // because current_item_use changes immediately back
                    // to NOT_USING and network variable changes are only
                    // sent at the end of the frame.
                    // If you want on_use_start to be called on remote clients
                    // then return use_result.underway from on_use_start
                    // and use_result.complete from on_use_continue.
                    if (has_authority)
                        current_item_use.value = (int)USE_TYPE.NOT_USING;
                }
            }
        };
    }

    public override void on_first_create()
    {
        // Create the inventory object
        client.create(transform.position, "inventories/player_inventory", this);
        client.create(transform.position, "inventories/player_crafting_menu", this);

        // Player starts in the middle of the first biome
        respawn_point.value = new Vector3(biome.SIZE / 2, world.SEA_LEVEL, biome.SIZE / 2);
        networked_position = respawn_point.value;
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

                foreach (var cs in inventory.ui.GetComponentsInChildren<color_selector>(true))
                    if (cs.name.Contains("hair"))
                    {
                        cs.color = net_hair_color.value;
                        cs.on_change = () => { net_hair_color.value = cs.color; };
                    }
            }
            else inventory = inv;
        }
    }

    public void lerp_towards(Vector3 position, float xrot, float yrot, float amt)
    {
        networked_position = Vector3.Lerp(networked_position, position, amt);
        x_rotation.value = Mathf.Lerp(x_rotation.value, xrot, amt);
        y_rotation.value = Mathf.Lerp(y_rotation.value, yrot, amt);
    }

    //################//
    // STATIC METHODS //
    //################//

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

// C# 8.0 allows default implementations in interfaces, once unity
// catches up with this, a lot of code in classes that implement
// ILeftPlayerMenu can be simplified.
public interface ILeftPlayerMenu
{
    /// <summary> The display name of whatever we're interating with. </summary>
    string left_menu_display_name();

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

    /// <summary> Additional recipes that the player
    /// can craft when this menu is open. </summary>
    recipe[] additional_recipes();
}

public interface ICustomMenu
{
    void open_custom_menu();
    void close_custom_menu();
}

/// <summary> Interfact for objects that can be 
/// left-clicked with no item equipped. </summary>
public interface IAcceptLeftClick
{
    void on_left_click();
    string left_click_context_tip();
}

/// <summary> Interfact for objects that can be 
/// right-clicked with no item equipped. </summary>
public interface IAcceptRightClick
{
    void on_right_click();
    string right_click_context_tip();
}