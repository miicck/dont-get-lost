using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class leg : MonoBehaviour
{
    public Transform foot;
    public Transform shin;
    public Transform thigh;
    public leg paired_with;

    Vector3 grounding;
    Vector3 step_point;
    Vector3 position_last;
    Vector3 transform_velocity;

    float thigh_length;
    float shin_length;

    bool moving_forward { get { return Vector3.Dot(transform_velocity, transform.forward) >= 0; } }

    float stride_length
    {
        get { return thigh_length + shin_length; }
    }

    Vector3 stride_centre
    {
        get { return transform.position + (moving_forward ? 1f : -1f) * stride_length * transform.forward / 4f; }
    }

    private void Start()
    {
        thigh_length = (thigh.transform.position - shin.transform.position).magnitude;
        shin_length = (shin.transform.position - foot.transform.position).magnitude;
        grounding = stride_centre;
        step_point = grounding;
        position_last = transform.position;
    }

    private void Update()
    {
        // Ensure I am out of phase with my paired leg
        if (paired_with != null)
        {
            Vector3 dg = paired_with.grounding - grounding;
            if (Vector3.Project(dg, transform.forward).magnitude < stride_length / 4f)
                grounding += transform.forward * stride_length / 2f;
        }

        // Store the velocity of the transform
        transform_velocity = (transform.position - position_last) / Time.deltaTime;
        position_last = transform.position;

        // Work out how fast the foot is allowed to move
        float max_foot_speed = transform_velocity.magnitude * 2f;
        if (max_foot_speed < 10e-4f) max_foot_speed = 10e-4f;

        // Work out if we need to take a step
        Vector3 delta = stride_centre - grounding;
        if (delta.magnitude > stride_length / 2f)
            grounding += delta.normalized * stride_length; // Take a step

        // Move the step_point along the line towards the grounding
        Vector3 disp = grounding - step_point;
        Vector3 move = disp;
        if (move.magnitude / Time.deltaTime > max_foot_speed)
            move = move.normalized * max_foot_speed * Time.deltaTime;
        step_point += move;

        // Work out how far through a step we are and set the foot raise
        // amount accordingly.
        Vector3 stride_amt = grounding - step_point;
        float raise_amt = Mathf.Sin(Mathf.PI * stride_amt.magnitude / stride_length);
        if (raise_amt < 0) raise_amt = 0;

        // The foot is only raised if it is moving relative to the ground.
        raise_amt *= (move.magnitude / Time.deltaTime) / max_foot_speed;

        // Set the foot position
        foot.transform.position = step_point + Vector3.up * raise_amt / 4f;

        // Work out the thigh/shin positions
        //shin.transform.position = (thigh.transform.position + foot.transform.position) / 2f;
        solve_leg();
    }

    void solve_leg()
    {
        float a = thigh_length;
        float b = shin_length;
        Vector3 dvec = foot.transform.position - thigh.transform.position;
        float d = dvec.magnitude;

        if (d > a + b)
        {
            shin.transform.position = thigh.transform.position + a * dvec / (a + b);
            return;
        }

        float lambda = d * d + b * b - a * a;
        lambda = b * b - lambda * lambda / (4 * d * d);
        lambda = Mathf.Sqrt(lambda);

        float d1 = a * a - lambda * lambda;
        d1 = Mathf.Sqrt(d1);

        shin.transform.position =
            thigh.transform.position +
            d1 * dvec.normalized -
            lambda * Vector3.Cross(transform.right, dvec.normalized);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(grounding, 0.05f);
        Vector3 delta = stride_centre - grounding;
        Gizmos.DrawLine(grounding, grounding + delta.normalized * stride_length);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(step_point, 0.05f);

        if (thigh != null && shin != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(thigh.transform.position, shin.transform.position);
        }

        if (shin != null && foot != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(shin.transform.position, foot.transform.position);
        }

        if (foot != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(foot.transform.position, foot.transform.position + foot.transform.forward / 4f);
        }
    }
}