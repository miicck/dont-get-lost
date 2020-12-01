using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Created when a world object is modified from it's default state. </summary>
public abstract class world_object_modifier : networked
{
    // The coordinates of the world object that this refers to
    // and the timeout for how long the modifier lasts for
    networked_variables.net_int x_in_chunk;
    networked_variables.net_int z_in_chunk;
    networked_variables.net_int chunk_x;
    networked_variables.net_int chunk_z;
    networked_variables.net_float timeout;

    public void target_to_world_object(world_object wo)
    {
        x_in_chunk.value = wo.x_in_chunk;
        z_in_chunk.value = wo.z_in_chunk;
        chunk_x.value = wo.chunk.x;
        chunk_z.value = wo.chunk.z;
    }

    public void set_timeout(float timeout)
    {
        this.timeout.value = timeout;
    }

    public override void on_init_network_variables()
    {
        x_in_chunk = new networked_variables.net_int();
        z_in_chunk = new networked_variables.net_int();
        chunk_x = new networked_variables.net_int();
        chunk_z = new networked_variables.net_int();
        timeout = new networked_variables.net_float();

        networked_variables.net_int.on_change_func on_change = () =>
        {
            CancelInvoke("on_locate");
            Invoke("on_locate", 0f);
        };

        x_in_chunk.on_change = on_change;
        z_in_chunk.on_change = on_change;
        chunk_x.on_change = on_change;
        chunk_z.on_change = on_change;

        timeout.on_change = () =>
        {
            Invoke("on_timeout", timeout.value);
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
        chunk.add_generation_listener(transform, chunk_x.value, chunk_z.value, (c) =>
        {
            target = c.get_object(x_in_chunk.value, z_in_chunk.value);
        });
    }

    /// <summary> Called when the timer runs out. </summary>
    void on_timeout() { delete(); }

    /// <summary> Untarget when destroyed. </summary>
    public override void on_forget(bool deleted) { target = null; }

    /// <summary> Called whenever the target world object changes. </summary>
    protected abstract void change_target(world_object old_target, world_object new_target);
}