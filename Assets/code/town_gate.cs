using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class town_gate : building_material
{
    public List<gate> gates = new List<gate>();
    public settler_path_element path_element;

    private void Start()
    {
        InvokeRepeating("spawn_settlers", 1, 1);
    }

    void spawn_settlers()
    {
        // Only spawn settlers on auth client
        if (!has_authority)
            return;

        var elements = settler_path_element.element_group(path_element.group);
        int bed_count = 0;
        foreach (var e in elements)
            if (e.interactable is bed)
                bed_count += 1;

        if (settler.settler_count < bed_count)
            client.create(transform.position, "characters/settler");
    }
}
