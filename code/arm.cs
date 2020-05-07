using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class arm : MonoBehaviour
{
    public Transform shoulder;
    public Transform elbow;
    public Transform wrist;

    public leg following;

    float bicep_length;
    float forearm_length;

    private void Start()
    {
        bicep_length = (elbow.transform.position - shoulder.transform.position).magnitude;
        forearm_length = (wrist.transform.position - elbow.transform.position).magnitude;
    }

    private void Update()
    {
        // Shoulder rotation is half of thigh rotation
        Quaternion thigh_rot = following.thigh.transform.rotation;
        Quaternion sholder_rot = Quaternion.Lerp(transform.rotation, thigh_rot, 0.5f);
        shoulder.transform.rotation = sholder_rot;

        // Elbow rotation is same as thigh rotation, but can't bend backwards
        elbow.transform.rotation = thigh_rot;
        if (Vector3.Dot(elbow.transform.up, transform.forward) > 0)
            elbow.transform.rotation = transform.rotation;
    }

    private void OnDrawGizmos()
    {
        if (shoulder != null && elbow != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(shoulder.transform.position, elbow.transform.position);
        }

        if (elbow != null && wrist != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(elbow.transform.position, wrist.transform.position);
        }
    }
}
