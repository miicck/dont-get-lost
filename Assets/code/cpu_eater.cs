using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cpu_eater : MonoBehaviour, INonBlueprintable, INonEquipable
{
    public static int j;
    void Update()
    {
        for (int i = 0; i < 1000000; ++i)
            j = i % 5;
    }
}
