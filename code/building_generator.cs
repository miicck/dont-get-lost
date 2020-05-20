using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class building_generator : world_object
{
    public const int MIN_FOOTPRINT = 4;
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
        int xsize = 2 * (footprint[0] / 2);
        int zsize = 2 * (footprint[1] / 2);
        int floors = Mathf.Min(xsize, zsize) / 2;
        int height = floors * 2;

        GameObject wall_section = Resources.Load<GameObject>("wall_sections/beam_wall_section");

        // North + south walls
        for (int x = 0; x < xsize; x += 2)
            for (int y = 0; y < floors; ++y)
            {
                var n = wall_section.inst();
                n.transform.SetParent(transform);
                n.transform.localPosition = new Vector3(x + 1, 2 * y, zsize);
                n.transform.localPosition += Random.insideUnitSphere / 100f; // Stop z fighting
                n.transform.forward = Vector3.forward;

                var s = wall_section.inst();
                s.transform.SetParent(transform);
                s.transform.localPosition = new Vector3(x + 1, 2 * y, 0);
                s.transform.localPosition += Random.insideUnitSphere / 100f; // Stop z fighting
                s.transform.forward = -Vector3.forward;
            }

        // East + west walls
        for (int z = 0; z < zsize; z += 2)
            for (int y = 0; y < floors; ++y)
            {
                var e = wall_section.inst();
                e.transform.SetParent(transform);
                e.transform.localPosition = new Vector3(xsize, 2 * y, z + 1);               
                e.transform.localPosition += Random.insideUnitSphere / 100f; // Stop z fighting
                e.transform.forward = Vector3.right;

                var w = wall_section.inst();
                w.transform.SetParent(transform);
                w.transform.localPosition = new Vector3(0, 2 * y, z + 1);
                w.transform.localPosition += Random.insideUnitSphere / 100f; // Stop z fighting
                w.transform.forward = -Vector3.right;
            }

        /*
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.transform.SetParent(transform);
        c.transform.localScale = new Vector3(xsize, height, zsize);
        c.transform.localPosition = c.transform.localScale / 2f;
        */
    }
}
