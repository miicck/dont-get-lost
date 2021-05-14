using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class controls
{
    public static float mouse_look_sensitivity = 1f;
    public static bool key_based_building = false;
    public static bool disabled = false;
    static Dictionary<BIND, control> keybinds = default_keybinds();

    static controls()
    {
        tips.add("You can switch between using the mouse or keyboard to rotate/translate buildings in the options menu.");
    }

    public abstract class control
    {
        public abstract bool triggered();   // Returns true on the frame that control is activated
        public abstract bool untriggered(); // Returns true on the frame that control is deactivated
        public abstract bool held();        // Returns true if the control is currently active
        public abstract string name();      // Returns human-readable name for control

        public abstract override bool Equals(object obj);
        public abstract override int GetHashCode();

        /// <summary> Returns the change in control this frame. By default is +1 
        /// if <see cref="triggered"/> this frame or -1 if <see cref="untriggered"/>
        /// this frame. Most useful for continuous axes (e.g. Mouse X/Y). </summary>
        public virtual float delta()
        {
            if (triggered()) return 1f;
            if (untriggered()) return -1f;
            return 0f;
        }
    }

    public class key_control : control
    {
        KeyCode key;
        public key_control(KeyCode key) { this.key = key; }
        public override bool triggered() { return Input.GetKeyDown(key); }
        public override bool untriggered() { return Input.GetKeyUp(key); }
        public override bool held() { return Input.GetKey(key); }

        public override int GetHashCode() { return key.GetHashCode(); }
        public override bool Equals(object obj)
        {
            if (obj is key_control)
                return ((key_control)obj).key == key;
            return false;
        }

        public override string name()
        {
            switch (key)
            {
                case KeyCode.Alpha0: return "0";
                case KeyCode.Alpha1: return "1";
                case KeyCode.Alpha2: return "2";
                case KeyCode.Alpha3: return "3";
                case KeyCode.Alpha4: return "4";
                case KeyCode.Alpha5: return "5";
                case KeyCode.Alpha6: return "6";
                case KeyCode.Alpha7: return "7";
                case KeyCode.Alpha8: return "8";
                case KeyCode.Alpha9: return "9";
                case KeyCode.UpArrow: return "the up key";
                case KeyCode.DownArrow: return "the down key";
                case KeyCode.LeftArrow: return "the left key";
                case KeyCode.RightArrow: return "the right arrow";
                case KeyCode.LeftControl: return "the control key";
                case KeyCode.LeftAlt: return "the alt key";
                case KeyCode.Space: return "the spacebar";
                case KeyCode.LeftShift: return "the shift key";
                default: return key.ToString();
            }
        }
    }

    public class mouse_control : control
    {
        public enum BUTTON : int
        {
            LEFT = 0,
            RIGHT = 1,
            MIDDLE = 2,
        }

        BUTTON button;
        public mouse_control(BUTTON button) { this.button = button; }
        public override bool triggered() { return Input.GetMouseButtonDown((int)button); }
        public override bool untriggered() { return Input.GetMouseButtonUp((int)button); }
        public override bool held() { return Input.GetMouseButton((int)button); }

        public override int GetHashCode() { return button.GetHashCode(); }
        public override bool Equals(object obj)
        {
            if (obj is mouse_control)
                return ((mouse_control)obj).button == button;
            return false;
        }

        public override string name()
        {
            switch (button)
            {
                case BUTTON.LEFT: return "left click";
                case BUTTON.RIGHT: return "right click";
                case BUTTON.MIDDLE: return "middle click";
                default: throw new System.Exception("Unkown mouse button!");
            }
        }
    }

    public class axis_control : control
    {
        string axis;
        public axis_control(string axis) { this.axis = axis; }
        public const float AXIS_EPS = 1e-5f;

        public override float delta() { return Input.GetAxis(axis); }
        public override bool triggered() { return Mathf.Abs(delta()) > AXIS_EPS; }
        public override bool untriggered() { return !triggered(); }
        public override bool held() { return triggered(); }
        public override string name() { return axis; }

        public override int GetHashCode() { return axis.GetHashCode(); }
        public override bool Equals(object obj)
        {
            if (obj is axis_control)
                return ((axis_control)obj).axis == axis;
            return false;
        }
    }

    public enum BIND
    {
        WALK_FORWARD,
        WALK_BACKWARD,
        STRAFE_LEFT,
        STRAFE_RIGHT,
        LOOK_LEFT_RIGHT,
        LOOK_UP_DOWN,
        HOME_TELEPORT,
        PLACE_MARKER,
        OPEN_INVENTORY,
        OPEN_RECIPE_BOOK,
        OPEN_TASK_MANAGER,
        LEAVE_MENU,
        USE_ITEM,
        PICK_UP_ITEM,
        PLACE_ON_GUTTER,
        ALT_USE_ITEM,
        SELECT_ITEM_FROM_WORLD,
        UNDO,
        REDO,
        CYCLE_QUICKBAR,
        QUICKBAR_1,
        QUICKBAR_2,
        QUICKBAR_3,
        QUICKBAR_4,
        QUICKBAR_5,
        QUICKBAR_6,
        QUICKBAR_7,
        QUICKBAR_8,
        GIVE,
        INSPECT,
        CROUCH,
        SINK,
        PAUSE_ON_LADDER,
        JUMP,
        FLY_UP,
        FLY_DOWN,
        ADD_CINEMATIC_KEYFRAME,
        REMOVE_LAST_CINEMATIC_KEYFRAME,
        TOGGLE_CINEMATIC_PLAYBACK,
        SLOW_WALK,
        TOGGLE_THIRD_PERSON,
        TOGGLE_MAP,
        ZOOM_MAP,
        CRAFT_FIVE,
        QUICK_ITEM_TRANSFER,
        OPEN_CONSOLE,
        CLOSE_CONSOLE,
        REPEAT_LAST_CONSOLE_COMMAND,
        CONSOLE_MOVE_HISTORY_BACK,
        CONSOLE_MOVE_HISTORY_FORWARD,
        TOGGLE_OPTIONS,
        TOGGLE_TECH_TREE,
        TOGGLE_PRODUCTION_INFO,
        TOGGLE_DEBUG_INFO,
        INCREASE_RENDER_RANGE,
        DECREASE_RENDER_RANGE,
        CHANGE_PIVOT,
        INCREMENT_PIVOT,
        FINE_ROTATION,
        ROTATE_ALSO_AXES,
        ROTATION_AMOUNT_X,
        ROTATION_AMOUNT_Y,
        MANIPULATE_BUILDING_DOWN,
        MANIPULATE_BUILDING_UP,
        MANIPULATE_BUILDING_BACK,
        MANIPULATE_BUILDING_FORWARD,
        MANIPULATE_BUILDING_RIGHT,
        MANIPULATE_BUILDING_LEFT,
        BUILDING_TRANSLATION,
        IGNORE_SNAP_POINTS,
        CYCLE_FULLSCREEN_MODES,
        ENTER_EXIT_CAVE,
        GET_NETWORK_INFO
    }

    static Dictionary<BIND, control> default_keybinds()
    {
        return new Dictionary<BIND, control>
        {
            [BIND.WALK_FORWARD] = new key_control(KeyCode.W),
            [BIND.WALK_BACKWARD] = new key_control(KeyCode.S),
            [BIND.STRAFE_LEFT] = new key_control(KeyCode.A),
            [BIND.STRAFE_RIGHT] = new key_control(KeyCode.D),
            [BIND.LOOK_LEFT_RIGHT] = new axis_control("Mouse X"),
            [BIND.LOOK_UP_DOWN] = new axis_control("Mouse Y"),
            [BIND.HOME_TELEPORT] = new key_control(KeyCode.H),
            [BIND.PLACE_MARKER] = new mouse_control(mouse_control.BUTTON.MIDDLE),
            [BIND.OPEN_INVENTORY] = new key_control(KeyCode.E),
            [BIND.OPEN_TASK_MANAGER] = new key_control(KeyCode.T),
            [BIND.LEAVE_MENU] = new key_control(KeyCode.Escape),
            [BIND.OPEN_RECIPE_BOOK] = new key_control(KeyCode.R),
            [BIND.USE_ITEM] = new mouse_control(mouse_control.BUTTON.LEFT),
            [BIND.PICK_UP_ITEM] = new mouse_control(mouse_control.BUTTON.LEFT),
            [BIND.PLACE_ON_GUTTER] = new mouse_control(mouse_control.BUTTON.RIGHT),
            [BIND.ALT_USE_ITEM] = new mouse_control(mouse_control.BUTTON.RIGHT),
            [BIND.SELECT_ITEM_FROM_WORLD] = new key_control(KeyCode.Q),
            [BIND.UNDO] = new key_control(KeyCode.Z),
            [BIND.REDO] = new key_control(KeyCode.X),
            [BIND.CYCLE_QUICKBAR] = new axis_control("Mouse ScrollWheel"),
            [BIND.QUICKBAR_1] = new key_control(KeyCode.Alpha1),
            [BIND.QUICKBAR_2] = new key_control(KeyCode.Alpha2),
            [BIND.QUICKBAR_3] = new key_control(KeyCode.Alpha3),
            [BIND.QUICKBAR_4] = new key_control(KeyCode.Alpha4),
            [BIND.QUICKBAR_5] = new key_control(KeyCode.Alpha5),
            [BIND.QUICKBAR_6] = new key_control(KeyCode.Alpha6),
            [BIND.QUICKBAR_7] = new key_control(KeyCode.Alpha7),
            [BIND.QUICKBAR_8] = new key_control(KeyCode.Alpha8),
            [BIND.GIVE] = new key_control(KeyCode.Q),
            [BIND.INSPECT] = new key_control(KeyCode.Tab),
            [BIND.CROUCH] = new key_control(KeyCode.LeftControl),
            [BIND.SINK] = new key_control(KeyCode.LeftShift),
            [BIND.PAUSE_ON_LADDER] = new key_control(KeyCode.LeftShift),
            [BIND.JUMP] = new key_control(KeyCode.Space),
            [BIND.FLY_UP] = new key_control(KeyCode.Space),
            [BIND.FLY_DOWN] = new key_control(KeyCode.LeftShift),
            [BIND.ADD_CINEMATIC_KEYFRAME] = new mouse_control(mouse_control.BUTTON.LEFT),
            [BIND.REMOVE_LAST_CINEMATIC_KEYFRAME] = new mouse_control(mouse_control.BUTTON.RIGHT),
            [BIND.TOGGLE_CINEMATIC_PLAYBACK] = new key_control(KeyCode.C),
            [BIND.SLOW_WALK] = new key_control(KeyCode.LeftShift),
            [BIND.TOGGLE_THIRD_PERSON] = new key_control(KeyCode.V),
            [BIND.TOGGLE_MAP] = new key_control(KeyCode.M),
            [BIND.ZOOM_MAP] = new axis_control("Mouse ScrollWheel"),
            [BIND.CRAFT_FIVE] = new key_control(KeyCode.LeftShift),
            [BIND.QUICK_ITEM_TRANSFER] = new key_control(KeyCode.LeftShift),
            [BIND.OPEN_CONSOLE] = new key_control(KeyCode.Slash),
            [BIND.CLOSE_CONSOLE] = new key_control(KeyCode.Escape),
            [BIND.REPEAT_LAST_CONSOLE_COMMAND] = new key_control(KeyCode.F1),
            [BIND.CONSOLE_MOVE_HISTORY_BACK] = new key_control(KeyCode.UpArrow),
            [BIND.CONSOLE_MOVE_HISTORY_FORWARD] = new key_control(KeyCode.DownArrow),
            [BIND.TOGGLE_OPTIONS] = new key_control(KeyCode.Escape),
            [BIND.TOGGLE_TECH_TREE] = new key_control(KeyCode.T),
            [BIND.TOGGLE_PRODUCTION_INFO] = new key_control(KeyCode.P),
            [BIND.TOGGLE_DEBUG_INFO] = new key_control(KeyCode.F3),
            [BIND.INCREASE_RENDER_RANGE] = new key_control(KeyCode.Equals),
            [BIND.DECREASE_RENDER_RANGE] = new key_control(KeyCode.Minus),
            [BIND.CHANGE_PIVOT] = new axis_control("Mouse ScrollWheel"),
            [BIND.INCREMENT_PIVOT] = new key_control(KeyCode.C),
            [BIND.FINE_ROTATION] = new key_control(KeyCode.LeftControl),
            [BIND.ROTATE_ALSO_AXES] = new key_control(KeyCode.LeftShift),
            [BIND.ROTATION_AMOUNT_X] = new axis_control("Mouse X"),
            [BIND.ROTATION_AMOUNT_Y] = new axis_control("Mouse Y"),
            [BIND.MANIPULATE_BUILDING_DOWN] = new key_control(KeyCode.Q),
            [BIND.MANIPULATE_BUILDING_UP] = new key_control(KeyCode.E),
            [BIND.MANIPULATE_BUILDING_BACK] = new key_control(KeyCode.S),
            [BIND.MANIPULATE_BUILDING_FORWARD] = new key_control(KeyCode.W),
            [BIND.MANIPULATE_BUILDING_RIGHT] = new key_control(KeyCode.D),
            [BIND.MANIPULATE_BUILDING_LEFT] = new key_control(KeyCode.A),
            [BIND.BUILDING_TRANSLATION] = new key_control(KeyCode.LeftAlt),
            [BIND.IGNORE_SNAP_POINTS] = new key_control(KeyCode.LeftControl),
            [BIND.CYCLE_FULLSCREEN_MODES] = new key_control(KeyCode.F11),
            [BIND.ENTER_EXIT_CAVE] = new mouse_control(mouse_control.BUTTON.LEFT),
            [BIND.GET_NETWORK_INFO] = new key_control(KeyCode.F2)
        };
    }

    /// <summary> Returns true if the given bind was triggered this frame. </summary>
    public static bool triggered(BIND bind)
    {
        return forward_activtity(keybinds[bind].triggered()) &&
               bind_enabled(bind);
    }

    /// <summary> Returns true if the given bind was untriggered this frame. </summary>
    public static bool untriggered(BIND bind)
    {
        return forward_activtity(keybinds[bind].untriggered()) &&
               bind_enabled(bind);
    }

    /// <summary> Returns true if the given bind is held down. </summary>
    public static bool held(BIND bind)
    {
        return forward_activtity(keybinds[bind].held()) &&
               bind_enabled(bind);
    }

    /// <summary> Returns the amount that the given control changed this frame. </summary>
    public static float delta(BIND bind)
    {
        float ret = forward_activtity(keybinds[bind].delta());
        if (!bind_enabled(bind)) return 0f;
        return ret;
    }

    /// <summary> Returns a human-readable name for the current bind (e.g "left click"). </summary>
    public static string bind_name(BIND b)
    {
        return keybinds[b].name();
    }

    public static control current_control(BIND b)
    {
        return keybinds[b];
    }

    /// <summary> If <paramref name="value"/> is true, then will tell the 
    /// client that it is active. Returns <paramref name="value"/>. </summary>
    static bool forward_activtity(bool value)
    {
        if (value) client.register_activity();
        return value;
    }

    /// <summary> Forwards activitiy to the client if <paramref name="delta"/> 
    /// is nonzero. Returns <paramref name="delta"/> </summary>
    static float forward_activtity(float delta)
    {
        if (Mathf.Abs(delta) > axis_control.AXIS_EPS) client.register_activity();
        return delta;
    }

    /// <summary> Returns true if the given bind is enabled. </summary>
    static bool bind_enabled(BIND b)
    {
        if (!is_player_control(b)) return true;
        return player_controls_enabled();
    }

    /// <summary> Returns true if the given keybind is related 
    /// to the player (rather than the UI etc.) </summary>
    static bool is_player_control(BIND b)
    {
        switch (b)
        {
            // Console commands aren't player commands
            case BIND.CONSOLE_MOVE_HISTORY_BACK:
            case BIND.CONSOLE_MOVE_HISTORY_FORWARD:
            case BIND.OPEN_CONSOLE:
            case BIND.REPEAT_LAST_CONSOLE_COMMAND:
                return false;

            // Everything else is a player command
            default:
                return true;
        }
    }

    /// <summary> Returns true if controls related to the player are enabled. </summary>
    static bool player_controls_enabled()
    {
        if (disabled) return false;
        if (ui_focus_disables_controls.controls_disabled) return false;
        if (console.open) return false;
        if (player.current != null && player.current.is_dead) return false;
        return true;
    }
}
