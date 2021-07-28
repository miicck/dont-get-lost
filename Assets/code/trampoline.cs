using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class trampoline : MonoBehaviour
{
    RaycastHit last_hit;

    public void bounce_player(player p, RaycastHit hit)
    {
        last_hit = hit;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(last_hit.point, last_hit.point + last_hit.normal);
    }
}
