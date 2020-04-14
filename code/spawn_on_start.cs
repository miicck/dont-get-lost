using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spawn_on_start : MonoBehaviour
{
    public GameObject to_spawn;
    public int count;
    public float time_betwen_spawns = 0;

    void spawn()
    {
        --count;
        to_spawn.inst().transform.position = transform.position;

        if (count <= 0)
            CancelInvoke();
    }

    void Start()
    {
        InvokeRepeating("spawn", 0, time_betwen_spawns);
    }
}
