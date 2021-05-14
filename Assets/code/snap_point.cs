using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class snap_point : MonoBehaviour, INonLogistical
{
    public Vector3[] snap_directions_90()
    {
        return new Vector3[]
        {
            transform.up, transform.right, transform.forward,
            -transform.up, -transform.right, -transform.forward
        };
    }

    public Vector3[] snap_directions_45()
    {
        Vector3[] ret = new Vector3[]
        {
            new Vector3( 1, 0, 0), new Vector3( 0, 1, 0), new Vector3( 0, 0, 1),
            new Vector3(-1, 0, 0), new Vector3( 0,-1, 0), new Vector3( 0, 0,-1),
            new Vector3( 0, 1, 1), new Vector3( 1, 0, 1), new Vector3( 1, 1, 0),
            new Vector3( 0,-1, 1), new Vector3(-1, 0, 1), new Vector3(-1, 1, 0),
            new Vector3( 0, 1,-1), new Vector3( 1, 0,-1), new Vector3( 1,-1, 0),
            new Vector3( 0,-1,-1), new Vector3(-1, 0,-1), new Vector3(-1,-1, 0),
        };

        for (int i = 0; i < ret.Length; ++i)
            ret[i] = transform.rotation * ret[i].normalized;

        return ret;
    }
}