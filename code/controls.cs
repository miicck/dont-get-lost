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

    public class keybinds
    {
        public KeyCode walk_forward = KeyCode.W;
        public KeyCode walk_backward = KeyCode.S;
        public KeyCode strafe_left = KeyCode.A;
        public KeyCode strafe_right = KeyCode.D;
        public KeyCode home_teleport = KeyCode.H;
        public KeyCode open_inventory = KeyCode.E;
        public KeyCode open_recipe_book = KeyCode.R;
        public KeyCode select_item_from_world = KeyCode.Q;
        public KeyCode quickbar_1 = KeyCode.Alpha1;
        public KeyCode quickbar_2 = KeyCode.Alpha2;
        public KeyCode quickbar_3 = KeyCode.Alpha3;
        public KeyCode quickbar_4 = KeyCode.Alpha4;
        public KeyCode quickbar_5 = KeyCode.Alpha5;
        public KeyCode quickbar_6 = KeyCode.Alpha6;
        public KeyCode quickbar_7 = KeyCode.Alpha7;
        public KeyCode quickbar_8 = KeyCode.Alpha8;
        public KeyCode inspect = KeyCode.Tab;
        public KeyCode crouch = KeyCode.LeftShift;
        public KeyCode sink = KeyCode.LeftShift;
        public KeyCode pause_on_ladder = KeyCode.LeftShift;
        public KeyCode jump = KeyCode.Space;
        public KeyCode fly_up = KeyCode.Space;
        public KeyCode fly_down = KeyCode.LeftShift;
        public KeyCode slow_walk = KeyCode.LeftControl;
        public KeyCode toggle_third_person = KeyCode.V;
        public KeyCode toggle_map = KeyCode.M;
        public KeyCode craft_five = KeyCode.LeftShift;
        public KeyCode quick_item_transfer = KeyCode.LeftShift;
        public KeyCode toggle_console = KeyCode.BackQuote;
        public KeyCode toggle_options = KeyCode.Escape;
        public KeyCode toggle_debug_info = KeyCode.F3;
        public KeyCode increase_render_range = KeyCode.Equals;
        public KeyCode decrease_render_range = KeyCode.Minus;
        public KeyCode change_pivot = KeyCode.C;
        public KeyCode fine_rotation = KeyCode.LeftControl;
        public KeyCode rotate_anticlockwise_around_up = KeyCode.Q;
        public KeyCode rotate_clockwise_around_up = KeyCode.E;
        public KeyCode rotate_anticlockwise_around_right = KeyCode.S;
        public KeyCode rotate_clockwise_around_right = KeyCode.W;
        public KeyCode rotate_anticlockwise_around_forward = KeyCode.D;
        public KeyCode rotate_clockwise_around_forward = KeyCode.A;
        public KeyCode ignore_snap_points = KeyCode.LeftControl;
        public KeyCode cycle_fullscreen_modes = KeyCode.F11;
    }

    public static keybinds binds = new keybinds();

    public static bool key_press(KeyCode k)
    {
        return Input.GetKeyDown(k);
    }

    public static bool key_down(KeyCode k)
    {
        return Input.GetKey(k);
    }

    public static bool mouse_click(MOUSE_BUTTON button)
    {
        return Input.GetMouseButtonDown((int)button);
    }

    public static bool mouse_down(MOUSE_BUTTON button)
    {
        return Input.GetMouseButton((int)button);
    }

    public static float get_axis(string name)
    {
        return Input.GetAxis(name);
    }
}
