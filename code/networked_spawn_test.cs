using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class networked_spawn_test : spawned_by_world_object
{
    networked_variable.net_float scale;

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        scale = new networked_variable.net_float();
        scale.on_change = (s, f) =>
        {
            transform.localScale = Vector3.one * s;
        };
    }

    public override void on_first_spawn()
    {
        scale.value = Random.Range(0.2f, 5f);
    }
}