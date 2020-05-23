using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class networked_spawner_test : networked_object_spawner
{
    protected override string prefab_to_spawn()
    {
        return "spawners/chicken_nest";
    }
}
