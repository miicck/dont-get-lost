using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class transform_randomizer : world_object.sub_generator
{
    public float min_scale = 1f;
    public float max_scale = 1f;
    public bool random_y_rotation = false;

    public override void generate(biome.point point, chunk chunk, int x_in_chunk, int z_in_chunk)
    {
        transform.localScale *= chunk.random.range(min_scale, max_scale);
        if (random_y_rotation)
            transform.Rotate(0, chunk.random.range(0, 360), 0);
    }
}
