using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class farmyard_generator : world_object
{
    public override void on_placement()
    {
        var size = (int[])point.gen_info;

        var hut_4x8 = Resources.Load<GameObject>("buildings/hut_4x8");
        var barn_6x8 = Resources.Load<GameObject>("buildings/barn_6x8");

        for (int z = 0; z < size[1] - 9;)
        {
            int dz = 0;
            for (int x = 0; x < size[0] - 7;)
            {
                GameObject new_buidling;
                switch (chunk.random.range(0, 3))
                {
                    case 0:
                        new_buidling = hut_4x8.inst();
                        new_buidling.transform.SetParent(transform);
                        new_buidling.transform.localPosition = new Vector3(x, 0, z);
                        x += 5;
                        if (9 > dz) dz = 9;
                        break;

                    case 1:
                        new_buidling = barn_6x8.inst();
                        new_buidling.transform.SetParent(transform);
                        new_buidling.transform.localPosition = new Vector3(x, 0, z);
                        x += 7;
                        if (9 > dz) dz = 9;
                        break;

                    case 2:
                        x += 4;
                        if (4 > dz) dz = 4;
                        break;

                    default:
                        throw new System.Exception("Unkown farmyard building index!");
                }

            }
            z += dz;
        }
    }
}
