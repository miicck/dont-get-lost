using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class hanging_object : MonoBehaviour
{
    void Start()
    {
        Vector3 forward = Vector3.Cross(Vector3.up, transform.right);
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}
