using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class connected_building_requirement : MonoBehaviour
{
    public building_material building;

    public bool satisfied(int group)
    {
        foreach (var element in town_path_element.element_group(group))
        {
            var b = element.GetComponentInParent<building_material>();
            if (b == null)
                continue;

            if (b.name == building.name)
                return true;
        }

        return false;
    }
}
