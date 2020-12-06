using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class controls
{
    public static float mouse_look_sensitivity = 1f;
    public static bool key_based_building = false;

    public enum MOUSE_BUTTON : int
    {
        LEFT = 0,
        RIGHT = 1,
        MIDDLE = 2,
    }

    public enum BIND
    {
        WALK_FORWARD,
        WALK_BACKWARD,
        STRAFE_LEFT,
        STRAFE_RIGHT,
        HOME_TELEPORT,
        OPEN_INVENTORY,
        OPEN_RECIPE_BOOK,
        SELECT_ITEM_FROM_WORLD,
        UNDO,
        REDO,
        QUICKBAR_1,
        QUICKBAR_2,
        QUICKBAR_3,
        QUICKBAR_4,
        QUICKBAR_5,
        QUICKBAR_6,
        QUICKBAR_7,
        QUICKBAR_8,
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
        CRAFT_FIVE,
        QUICK_ITEM_TRANSFER,
        TOGGLE_CONSOLE,
        REPEAT_LAST_CONSOLE_COMMAND,
        CONSOLE_MOVE_HISTORY_BACK,
        CONSOLE_MOVE_HISTORY_FORWARD,
        TOGGLE_OPTIONS,
        TOGGLE_DEBUG_INFO,
        INCREASE_RENDER_RANGE,
        DECREASE_RENDER_RANGE,
        CHANGE_PIVOT,
        FINE_ROTATION,
        ROTATE_ANTICLOCKWISE_AROUND_UP,
        ROTATE_CLOCKWISE_AROUND_UP,
        ROTATE_ANTICLOCKWISE_AROUND_RIGHT,
        ROTATE_CLOCKWISE_AROUND_RIGHT,
        ROTATE_ANTICLOCKWISE_AROUND_FORWARD,
        ROTATE_CLOCKWISE_AROUND_FORWARD,
        BUILDING_TRANSLATION,
        TRANSLATE_RIGHT,
        TRANSLATE_LEFT,
        TRANSLATE_UP,
        TRANSLATE_DOWN,
        TRANSLATE_FORWARD,
        TRANSLATE_BACK,
        IGNORE_SNAP_POINTS,
        CYCLE_FULLSCREEN_MODES
    }

    static Dictionary<BIND, KeyCode> default_keybinds()
    {
        return new Dictionary<BIND, KeyCode>
        {
            [BIND.WALK_FORWARD] = KeyCode.W,
            [BIND.WALK_BACKWARD] = KeyCode.S,
            [BIND.STRAFE_LEFT] = KeyCode.A,
            [BIND.STRAFE_RIGHT] = KeyCode.D,
            [BIND.HOME_TELEPORT] = KeyCode.H,
            [BIND.OPEN_INVENTORY] = KeyCode.E,
            [BIND.OPEN_RECIPE_BOOK] = KeyCode.R,
            [BIND.SELECT_ITEM_FROM_WORLD] = KeyCode.Q,
            [BIND.UNDO] = KeyCode.Z,
            [BIND.REDO] = KeyCode.X,
            [BIND.QUICKBAR_1] = KeyCode.Alpha1,
            [BIND.QUICKBAR_2] = KeyCode.Alpha2,
            [BIND.QUICKBAR_3] = KeyCode.Alpha3,
            [BIND.QUICKBAR_4] = KeyCode.Alpha4,
            [BIND.QUICKBAR_5] = KeyCode.Alpha5,
            [BIND.QUICKBAR_6] = KeyCode.Alpha6,
            [BIND.QUICKBAR_7] = KeyCode.Alpha7,
            [BIND.QUICKBAR_8] = KeyCode.Alpha8,
            [BIND.INSPECT] = KeyCode.Tab,
            [BIND.CROUCH] = KeyCode.LeftShift,
            [BIND.SINK] = KeyCode.LeftShift,
            [BIND.PAUSE_ON_LADDER] = KeyCode.LeftShift,
            [BIND.JUMP] = KeyCode.Space,
            [BIND.FLY_UP] = KeyCode.Space,
            [BIND.FLY_DOWN] = KeyCode.LeftShift,
            [BIND.ADD_CINEMATIC_KEYFRAME] = KeyCode.K,
            [BIND.REMOVE_LAST_CINEMATIC_KEYFRAME] = KeyCode.J,
            [BIND.TOGGLE_CINEMATIC_PLAYBACK] = KeyCode.P,
            [BIND.SLOW_WALK] = KeyCode.LeftControl,
            [BIND.TOGGLE_THIRD_PERSON] = KeyCode.V,
            [BIND.TOGGLE_MAP] = KeyCode.M,
            [BIND.CRAFT_FIVE] = KeyCode.LeftShift,
            [BIND.QUICK_ITEM_TRANSFER] = KeyCode.LeftShift,
            [BIND.TOGGLE_CONSOLE] = KeyCode.Slash,
            [BIND.REPEAT_LAST_CONSOLE_COMMAND] = KeyCode.F1,
            [BIND.CONSOLE_MOVE_HISTORY_BACK] = KeyCode.UpArrow,
            [BIND.CONSOLE_MOVE_HISTORY_FORWARD] = KeyCode.DownArrow,
            [BIND.TOGGLE_OPTIONS] = KeyCode.Escape,
            [BIND.TOGGLE_DEBUG_INFO] = KeyCode.F3,
            [BIND.INCREASE_RENDER_RANGE] = KeyCode.Equals,
            [BIND.DECREASE_RENDER_RANGE] = KeyCode.Minus,
            [BIND.CHANGE_PIVOT] = KeyCode.C,
            [BIND.FINE_ROTATION] = KeyCode.LeftControl,
            [BIND.ROTATE_ANTICLOCKWISE_AROUND_UP] = KeyCode.Q,
            [BIND.ROTATE_CLOCKWISE_AROUND_UP] = KeyCode.E,
            [BIND.ROTATE_ANTICLOCKWISE_AROUND_RIGHT] = KeyCode.S,
            [BIND.ROTATE_CLOCKWISE_AROUND_RIGHT] = KeyCode.W,
            [BIND.ROTATE_ANTICLOCKWISE_AROUND_FORWARD] = KeyCode.D,
            [BIND.ROTATE_CLOCKWISE_AROUND_FORWARD] = KeyCode.A,
            [BIND.BUILDING_TRANSLATION] = KeyCode.LeftAlt,
            [BIND.TRANSLATE_RIGHT] = KeyCode.D,
            [BIND.TRANSLATE_LEFT] = KeyCode.A,
            [BIND.TRANSLATE_FORWARD] = KeyCode.W,
            [BIND.TRANSLATE_BACK] = KeyCode.S,
            [BIND.TRANSLATE_UP] = KeyCode.E,
            [BIND.TRANSLATE_DOWN] = KeyCode.Q,
            [BIND.IGNORE_SNAP_POINTS] = KeyCode.LeftControl,
            [BIND.CYCLE_FULLSCREEN_MODES] = KeyCode.F11,
        };
    }

    public static bool disabled = false;

    static bool forward_activtity(bool value)
    {
        if (value) client.register_activity();
        return value;
    }

    static bool is_player_control(BIND b)
    {
        switch (b)
        {
            // Console commands aren't player commands
            case BIND.CONSOLE_MOVE_HISTORY_BACK:
            case BIND.CONSOLE_MOVE_HISTORY_FORWARD:
            case BIND.TOGGLE_CONSOLE:
            case BIND.REPEAT_LAST_CONSOLE_COMMAND:
                return false;

            // Everything else is a player command
            default:
                return true;
        }
    }

    static bool player_controls_enabled()
    {
        if (disabled) return false;
        if (ui_focus_disables_controls.controls_disabled) return false;
        if (console.open) return false;
        if (player.current != null && player.current.is_dead) return false;
        return true;
    }

    static bool bind_enabled(BIND b)
    {
        if (!is_player_control(b)) return true;
        return player_controls_enabled();
    }

    static bool mouse_enabled(MOUSE_BUTTON b)
    {
        return player_controls_enabled();
    }

    static bool axis_enabled(string name)
    {
        return player_controls_enabled();
    }

    static Dictionary<BIND, KeyCode> keybinds = default_keybinds();

    public static bool key_press(BIND k)
    {
        if (!bind_enabled(k)) return false;
        return forward_activtity(Input.GetKeyDown(keybinds[k]));
    }

    public static bool key_down(BIND k)
    {
        if (!bind_enabled(k)) return false;
        return forward_activtity(Input.GetKey(keybinds[k]));
    }

    public static bool mouse_click(MOUSE_BUTTON button)
    {
        if (!mouse_enabled(button)) return false;
        return forward_activtity(Input.GetMouseButtonDown((int)button));
    }

    public static bool mouse_unclick(MOUSE_BUTTON button)
    {
        if (!mouse_enabled(button)) return false;
        return forward_activtity(Input.GetMouseButtonUp((int)button));
    }

    public static bool mouse_down(MOUSE_BUTTON button)
    {
        if (!mouse_enabled(button)) return false;
        return forward_activtity(Input.GetMouseButton((int)button));
    }

    public static float object_rotation_axis()
    {
        return Input.GetAxis("Mouse X") + Input.GetAxis("Mouse Y");
    }

    public static float get_axis(string name)
    {
        if (!axis_enabled(name)) return 0;
        return Input.GetAxis(name);
    }
}
