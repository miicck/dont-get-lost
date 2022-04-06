using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class workshop_requirement : MonoBehaviour
{
    public workshop workshop;

    public bool satisfied(int group)
    {
        foreach (var element in town_path_element.element_group(group))
        {
            var shop = element.GetComponentInParent<workshop>();
            if (shop == null)
                continue;

            if (shop.name == workshop.name)
                if (shop.operational)
                    return true;
        }

        return false;
    }
}
