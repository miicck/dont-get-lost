using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class town_gate : MonoBehaviour
{
    public List<gate> gates = new List<gate>();
    public settler_path_element path_element;

    private void Start()
    {
        InvokeRepeating("spawn_settlers", 1, 1);
    }

    void spawn_settlers()
    {
        var elements = settler_path_element.element_group(path_element.group);
    }
}
