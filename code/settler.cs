using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A memeber of a settlment. </summary>
public class settler : pathfinding_agent, IInspectable
{
    public bed bed { get => GetComponentInParent<bed>(); }
    item_requirement[] requirements { get => GetComponents<item_requirement>(); }

    /// <summary> Check if a set of fixtures is enough 
    /// for this settler to move in. </summary>
    public bool requirements_satisfied(IEnumerable<fixture> fixtures)
    {
        foreach (var r in requirements)
        {
            bool satisfied = false;
            foreach (var f in fixtures)
                if (r.satisfied(f))
                {
                    satisfied = true;
                    break;
                }

            if (!satisfied)
                return false;
        }

        return true;
    }

    protected override bool path_constriant(Vector3 v)
    {
        return (v - bed.transform.position).magnitude < 5f;
    }

    void idle()
    {
        go_to(random_target(5f), on_fail: idle, on_arrive: idle);
    }

    public override void on_gain_authority()
    {
        base.on_gain_authority();

        idle();
    }

    public string inspect_info()
    {
        return name;
    }

    public Sprite main_sprite()
    {
        return null;
    }

    public Sprite secondary_sprite()
    {
        return null;
    }
}