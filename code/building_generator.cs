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

    GameObject select_face_object(COMPASS_DIRECTION direction, int n, int y, int n_door)
    {
        GameObject ret = wall_sections[chunk.random.range(0, wall_sections.Count)];
        if (direction == front)
        {
            if (n / 2 % 2 == 0 && (windows_on_odd_floors || y % 2 == 0))
                ret = window_sections[chunk.random.range(0, window_sections.Count)];

            if (y == 0 && n == n_door)
                ret = door_sections[chunk.random.range(0, door_sections.Count)];
        }
        return ret;
    }

    COMPASS_DIRECTION front;
    int xsize;
    int zsize;
    int floors;
    bool windows_on_odd_floors;

    public List<GameObject> wall_sections = new List<GameObject>();
    public List<GameObject> window_sections = new List<GameObject>();
    public List<GameObject> door_sections = new List<GameObject>();
    public List<GameObject> roofs = new List<GameObject>();

    public override void on_placement()
    {
        var info_objects = (object[])point.gen_info;

        xsize = 2 * ((int)info_objects[0] / 2);
        zsize = 2 * ((int)info_objects[1] / 2);
        front = (COMPASS_DIRECTION)info_objects[2];
        floors = Mathf.Min(xsize, zsize) / 2;
        windows_on_odd_floors = chunk.random.range(0, 2) == 0;

        int x_door = 2 * (xsize / 4);
        int z_door = 2 * (zsize / 4);

        // North face
        for (int x = 0; x < xsize; x += 2)
            for (int y = 0; y < floors; ++y)
            {
                GameObject to_gen = select_face_object(COMPASS_DIRECTION.NORTH, x, y, x_door);

                var n = to_gen.inst();
                n.transform.SetParent(transform);
                n.transform.localPosition = new Vector3(x + 1, 2 * y, zsize);
                n.transform.localScale *= chunk.random.range(1f, 1.001f); // Stop z fighting
                n.transform.forward = Vector3.forward;
            }

        // South face
        for (int x = 0; x < xsize; x += 2)
            for (int y = 0; y < floors; ++y)
            {
                GameObject to_gen = select_face_object(COMPASS_DIRECTION.SOUTH, x, y, x_door);

                var s = to_gen.inst();
                s.transform.SetParent(transform);
                s.transform.localPosition = new Vector3(x + 1, 2 * y, 0);
                s.transform.localScale *= chunk.random.range(1f, 1.001f); // Stop z fighting
                s.transform.forward = -Vector3.forward;
            }

        // East face
        for (int z = 0; z < zsize; z += 2)
            for (int y = 0; y < floors; ++y)
            {
                GameObject to_gen = select_face_object(COMPASS_DIRECTION.EAST, z, y, z_door);

                var e = to_gen.inst();
                e.transform.SetParent(transform);
                e.transform.localPosition = new Vector3(xsize, 2 * y, z + 1);
                e.transform.localScale *= chunk.random.range(1f, 1.001f); // Stop z fighting
                e.transform.forward = Vector3.right;
            }

        // West face
        for (int z = 0; z < zsize; z += 2)
            for (int y = 0; y < floors; ++y)
            {
                GameObject to_gen = select_face_object(COMPASS_DIRECTION.WEST, z, y, z_door);

                var w = to_gen.inst();
                w.transform.SetParent(transform);
                w.transform.localPosition = new Vector3(0, 2 * y, z + 1);
                w.transform.localScale *= chunk.random.range(1f, 1.001f); // Stop z fighting
                w.transform.forward = -Vector3.right;
            }

        // Create the roof
        GameObject roof = roofs[chunk.random.range(0, roofs.Count)].inst();
        roof.transform.SetParent(transform);
        int min_size = Mathf.Min(xsize, zsize);
        int max_size = Mathf.Max(xsize, zsize);

        roof.transform.localScale = new Vector3(min_size, min_size, max_size);
        roof.transform.localPosition = new Vector3(0, floors * 2, 0);
        roof.transform.forward = Vector3.forward;
        if (xsize > zsize)
        {
            roof.transform.forward = Vector3.right;
            roof.transform.localPosition += Vector3.forward * zsize;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(building_generator))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }
    }
#endif
}
