using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Created when a world object is modified from it's default state. </summary>
public abstract class world_object_modifier : networked
{
    public networked_variable.net_int x_in_chunk;
    public networked_variable.net_int z_in_chunk;
    public networked_variable.net_float timeout;

    public override void on_init_network_variables()
    {
        x_in_chunk = new networked_variable.net_int(-1);
        z_in_chunk = new networked_variable.net_int(-1);
        timeout = new networked_variable.net_float();

        x_in_chunk.on_change = (x, f) =>
        {
            CancelInvoke("on_locate");
            Invoke("on_locate", 0f);
        };

        z_in_chunk.on_change = (z, f) =>
        {
            CancelInvoke("on_locate");
            Invoke("on_locate", 0f);
        };

        timeout.on_change = (t, f) =>
        {
            Invoke("on_timeout", t);
        };
    }

    /// <summary> The world object I am modifiying. </summary>
    world_object target
    {
        get => _target;
        set
        {
            change_target(_target, value);
            _target = value;
        }
    }
    world_object _target;

    /// <summary> Called whenever the in_chunk coordinates change. </summary>
    void on_locate()
    {
        var c = chunk.at(transform.position, generated_only: true);

        if (c == null)
        {
            // Wait until the chunk has generated
            CancelInvoke("on_locate");
            Invoke("on_locate", 0f);
            return;
        }

        foreach (var wo in c.GetComponentsInChildren<world_object>())
            if (wo.x_in_chunk == x_in_chunk.value &&
                wo.z_in_chunk == z_in_chunk.value)
            {
                target = wo;
            }
    }

    /// <summary> Called when the timer runs out. </summary>
    void on_timeout() { delete(); }

    /// <summary> Untarget when destroyed. </summary>
    public override void on_forget() { target = null; }

    /// <summary> Called whenever the target world object changes. </summary>
    protected abstract void change_target(world_object old_target, world_object new_target);
}