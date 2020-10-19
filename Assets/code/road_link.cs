using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class road_link : MonoBehaviour
{
    public road_link linked_to;

    private void OnDrawGizmos()
    {
        if (linked_to == null)
            Gizmos.color = Color.red;
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, linked_to.transform.position);
        }
        Gizmos.DrawWireSphere(transform.position, 0.1f);
    }
}
