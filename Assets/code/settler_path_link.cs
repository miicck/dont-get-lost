using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_path_link : MonoBehaviour
{
    public const float LINK_DISTANCE = 0.25f;

    public settler_path_link linked_to;

    private void OnDrawGizmos()
    {
        if (linked_to == null)
            Gizmos.color = Color.red;
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, linked_to.transform.position);
        }
        Gizmos.DrawWireSphere(transform.position, LINK_DISTANCE / 2f);
    }
}
