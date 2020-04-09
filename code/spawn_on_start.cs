using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spawn_on_start : MonoBehaviour
{
    public GameObject to_spawn;
    public int count;
    public float time_betwen_spawns = 0;

    int count_spawned = 0;
    void spawn()
    {
        ++count_spawned;
        to_spawn.inst().transform.position = transform.position;

        if (count_spawned > count)
            CancelInvoke();
    }

    void Start()
    {
        InvokeRepeating("spawn", 0, time_betwen_spawns);
    }
}
