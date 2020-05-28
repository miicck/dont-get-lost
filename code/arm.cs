using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class arm : MonoBehaviour
{
    public Transform shoulder;
    public Transform elbow;
    public Transform hand;

    public leg following;

    public Transform to_grab;
    public bool elbow_bends_backwards = false;

    float bicep_length;
    float forearm_length;

    private void Start()
    {
        bicep_length = (elbow.transform.position - shoulder.transform.position).magnitude;
        forearm_length = (hand.transform.position - elbow.transform.position).magnitude;
    }

    /// <summary> The arm follows the leg, so it looks like we're running </summary>
    void update_with_leg()
    {
        if (following == null) return;

        // Shoulder rotation is half of thigh rotation
        Quaternion thigh_rot = following.hip.rotation;
        Quaternion sholder_rot = Quaternion.Lerp(transform.rotation, thigh_rot, 0.5f);
        shoulder.transform.rotation = sholder_rot;

        // Elbow rotation is same as thigh rotation, but can't bend backwards
        elbow.transform.rotation = thigh_rot;
        if (Vector3.Dot(elbow.transform.up, transform.forward) > 0)
            elbow.transform.rotation = transform.rotation;
    }

    /// <summary> The arm grabs grab_position. </summary>
    void update_to_grab()
    {
        float a = bicep_length;
        float b = forearm_length;
        Vector3 dvec = to_grab.position - shoulder.transform.position;
        float d = dvec.magnitude;

        Vector3 shoulder_elbow;

        if (d > a + b) // Overstretched
        {
            shoulder_elbow = dvec;
        }
        else
        {

            // Work out lambda
            float lambda = d * d + b * b - a * a;
            lambda = b * b - lambda * lambda / (4 * d * d);
            lambda = Mathf.Sqrt(lambda);

            // Work out d1
            float d1 = a * a - lambda * lambda;
            d1 = Mathf.Sqrt(d1);

            if (!elbow_bends_backwards)
                lambda = -lambda;

            shoulder_elbow = d1 * dvec.normalized -
                lambda * Vector3.Cross(transform.right, dvec.normalized);
        }

        shoulder.transform.rotation = Quaternion.LookRotation(
            Vector3.Cross(shoulder_elbow, transform.right), -shoulder_elbow
        );

        Vector3 elbow_wrist = to_grab.position - elbow.transform.position;
        elbow.transform.rotation = Quaternion.LookRotation(
            Vector3.Cross(elbow_wrist, transform.right), -elbow_wrist
        );
    }

    private void Update()
    {
        if (to_grab == null) update_with_leg();
        else update_to_grab();
    }

    private void OnDrawGizmos()
    {
        if (shoulder != null && elbow != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(shoulder.transform.position, elbow.transform.position);
        }

        if (to_grab != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(to_grab.position, 0.05f);
        }
    }
}
