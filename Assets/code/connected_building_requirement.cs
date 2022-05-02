using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class connected_building_requirement : MonoBehaviour
{
    public building_material building;

    public bool satisfied(int group)
    {
        building_material found = null;

        town_path_element.iterate_over_elements(group, (element) =>
        {
            var b = element.GetComponentInParent<building_material>();
            if (b == null)
                return false;

            if (b.name == building.name)
            {
                found = b;
                return true;
            }

            return false;
        });

        return found != null;
    }
}
