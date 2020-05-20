using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class building_generator : world_object
{
    public const int MIN_FOOTPRINT = 5;
    public const int MAX_FOOTPRINT = 16;

    public static int[] random_footprint(System.Random rand)
    {
        return new int[]
        {
            rand.range(MIN_FOOTPRINT, MAX_FOOTPRINT+1),
            rand.range(MIN_FOOTPRINT, MAX_FOOTPRINT+1)
        };
    }

    public override void on_placement(Vector3 terrain_normal, biome.point point, 
        chunk chunk, int x_in_chunk, int z_in_chunk)
    {
        var footprint = (int[])point.gen_info;
        int xsize = footprint[0];
        int zsize = footprint[1];
        int height = Mathf.Min(xsize, zsize);

        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.transform.SetParent(transform);
        c.transform.localScale = new Vector3(xsize, height, zsize);
        c.transform.localPosition = c.transform.localScale / 2f;
    }
}
