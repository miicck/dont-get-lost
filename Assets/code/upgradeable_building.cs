using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class upgradeable_building : MonoBehaviour
{
    public List<building_material> can_upgrade_to = new List<building_material>();

    public bool can_upgrade(building_material to)
    {
        if (to == null) return false;
        foreach (var b in can_upgrade_to)
            if (b.name == to.name)
                return true;
        return false;
    }
}
