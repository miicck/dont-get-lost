using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class farmyard_generator : world_object
{
    private void Start()
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.transform.SetParent(transform);
        c.transform.localPosition = new Vector3(0, 0.5f, 0);
    }
}
