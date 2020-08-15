using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class transform_path : MonoBehaviour
{
    public bool is_loop = false;

    public int waypoint_count
    {
        get
        {
            if (is_loop) 
                return transform.childCount + 1;
            return transform.childCount;
        }
    }

    public Transform waypoint(int n)
    {
        return transform.GetChild(n % transform.childCount);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        for (int i=1; i<waypoint_count; ++i)
            Gizmos.DrawLine(waypoint(i-1).position, waypoint(i).position);
    }
}
