using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class controls
{
    public enum MOUSE_BUTTON : int
    {
        LEFT = 0,
        RIGHT = 1,
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
        SLOW_WALK,
        TOGGLE_THIRD_PERSON,
        TOGGLE_MAP,
        CRAFT_FIVE,
        QUICK_ITEM_TRANSFER,
        TOGGLE_CONSOLE,
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
            [BIND.SLOW_WALK] = KeyCode.LeftControl,
            [BIND.TOGGLE_THIRD_PERSON] = KeyCode.V,
            [BIND.TOGGLE_MAP] = KeyCode.M,
            [BIND.CRAFT_FIVE] = KeyCode.LeftShift,
            [BIND.QUICK_ITEM_TRANSFER] = KeyCode.LeftShift,
            [BIND.TOGGLE_CONSOLE] = KeyCode.BackQuote,
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
            [BIND.IGNORE_SNAP_POINTS] = KeyCode.LeftControl,
            [BIND.CYCLE_FULLSCREEN_MODES] = KeyCode.F11,
        };
    }

    static bool controls_enabled()
    {
        if (ui_focus_disables_controls.controls_disabled) return false;
        if (console.open) return false;
        return true;
    }

    static bool bind_enabled(BIND b)
    {
        return controls_enabled();
    }

    static bool mouse_enabled(MOUSE_BUTTON b)
    {
        return controls_enabled();
    }

    static bool axis_enabled(string name)
    {
        return controls_enabled();
    }

    static Dictionary<BIND, KeyCode> keybinds = default_keybinds();

    public static bool key_press(BIND k)
    {
        if (!bind_enabled(k)) return false;
        return Input.GetKeyDown(keybinds[k]);
    }

    public static bool key_down(BIND k)
    {
        if (!bind_enabled(k)) return false;
        return Input.GetKey(keybinds[k]);
    }

    public static bool mouse_click(MOUSE_BUTTON button)
    {
        if (!mouse_enabled(button)) return false;
        return Input.GetMouseButtonDown((int)button);
    }

    public static bool mouse_down(MOUSE_BUTTON button)
    {
        if (!mouse_enabled(button)) return false;
        return Input.GetMouseButton((int)button);
    }

    public static float get_axis(string name)
    {
        if (!axis_enabled(name)) return 0;
        return Input.GetAxis(name);
    }
}
