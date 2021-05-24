using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class random_color_building_material : building_material
{
    public List<Renderer> set_color_on = new List<Renderer>();

    networked_variables.net_color color;

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();

        color = new networked_variables.net_color(Color.white);
        color.on_change = () =>
        {
            foreach (var r in set_color_on)
                utils.set_color(r.material, color.value);
        };
    }

    public override void on_build()
    {
        base.on_build();

        color.value = new Color(
            Random.Range(0, 1f),
            Random.Range(0, 1f),
            Random.Range(0, 1f)
        );
    }
}
