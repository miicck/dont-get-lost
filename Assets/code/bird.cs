using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bird : character
{
    protected override ICharacterController default_controller()
    {
        return base.default_controller();
        return new default_bird_controller();
    }
}

public class default_bird_controller : ICharacterController
{
    public void control(character c)
    {

    }

    public void on_end_control(character c)
    {

    }

    public void draw_gizmos() { }
    public void draw_inspector_gui() { }
    public string inspect_info() { return "A bird."; }
}
