using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class leg : MonoBehaviour
{
    public Transform character;  // The character to which these legs belong (stops the legs from stepping on colliders contained within the character itself)
    public Transform foot;       // The foot, with pivot set at the ankle
    public Transform shin;       // The shin, with pivot set at the knee
    public Transform thigh;      // The thigh, with pivot set at the hip
    public leg lead_by;          // The leg that this leg should be lead by (to keep the legs out of phase)

    Vector3 grounding;           // Point which the foot next makes contact with the ground
    Vector3 step_point;          // Current point along the line from the previous grounding to next grounding
    Vector3 position_last;       // The last position of leg.transform (used to calculate transform_velocity)
    Vector3 transform_velocity;  // The current velocity of leg.transform

    float thigh_length;          // The length of the thigh (calculated in start())
    float shin_length;           // The length of the shin (calculated in start())

    // The length of one step
    float stride_length { get { return thigh_length + shin_length; } }

    // The direction of the next step
    Vector3 step_direction
    {
        get
        {
            // Alligned with transform_velocity if possible, else just transform.forward
            if (transform_velocity.magnitude > 1e-5f) return transform_velocity.normalized;
            return transform.forward;
        }
    }

    // The centre of the current step (this is slightly in front of tranform.position so that it looks natural)
    Vector3 stride_centre { get { return transform.position + step_direction * stride_length / 4f; } }

    // The two points between which to raycast for solid ground
    Vector3[] test_start_end
    {
        get
        {
            Vector3 test_centre = stride_centre + step_direction * stride_length / 2f;
            Vector3 test_dir = -Vector3.Cross(transform.right, step_direction);

            return new Vector3[]
            {
                test_centre + test_dir,
                test_centre - test_dir
            };
        }
    }

    // Update the grounding point to the next grounding point (i.e start taking a step)
    void step()
    {
        var test = test_start_end;
        Vector3 delta = test[1] - test[0];

        // Look for solid ground between test[0] and test[1]
        foreach (var h in Physics.RaycastAll(test[0], delta.normalized, delta.magnitude))
        {
            if (h.collider.transform.IsChildOf(character))
                continue;
            grounding = h.point;
        }

        // None found, just set to midway
        grounding = (test[0] + test[1]) / 2f;
    }

    private void Start()
    {
        // Calculate thigh/shin lengths
        thigh_length = (thigh.transform.position - shin.transform.position).magnitude;
        shin_length = (shin.transform.position - foot.transform.position).magnitude;

        // Initialize step control points
        grounding = stride_centre;
        step_point = grounding;
        position_last = transform.position;
    }

    private void Update()
    {
        // Store the velocity of the transform
        transform_velocity = (transform.position - position_last) / Time.deltaTime;
        position_last = transform.position;

        // Work out how fast the foot is allowed to move
        float max_foot_speed = transform_velocity.magnitude * 2f;
        if (max_foot_speed < 10e-4f) max_foot_speed = 10e-4f;

        // Ensure I am out of phase with my paired leg
        if (lead_by != null)
        {
            float ahead_amt = Vector3.Dot(grounding - lead_by.grounding, step_direction) / stride_length;

            if (Mathf.Abs(ahead_amt) < 0.45f)
            {
                // Legs too close to in phase
                if (ahead_amt > 0)
                    grounding += step_direction * stride_length * (0.5f - ahead_amt);
                else
                    grounding -= step_direction * stride_length * (0.5f + ahead_amt);
            }
        }

        // Work out if we need to take a step
        Vector3 delta = stride_centre - grounding;
        if (delta.magnitude > stride_length / 2f)
            step(); // Take a step

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
        // Work out the position of the thigh and shin, given the current foot position
        // according to the following diagram.
        // 
        //                                         a = thigh_length = hip-to-knee distance
        //   thigh.transform.position = hip -> ._________. <- knee = shin.transform.position
        //                                     \        /
        //                                      \L     /  
        //                hip-ankle distance = d \    / b = shin_length = knee-to-ankle distance
        //                                        \  /
        //                                         \/. <- ankle = foot.transform.position
        //
        // point labelled L = the closest point to the knee along the hip-to-ankle line
        //           lambda = the distance between the knee and the point labelled L
        //               d1 = the distance between the hip  and the point labelled L

        float a = thigh_length;
        float b = shin_length;
        Vector3 dvec = foot.transform.position - thigh.transform.position;
        float d = dvec.magnitude;

        if (d > a + b)
        {
            // Foot is further than the maximum extent of the leg, create a streight leg
            shin.transform.position = thigh.transform.position + a * dvec / (a + b);
            return;
        }

        // Work out lambda
        float lambda = d * d + b * b - a * a;
        lambda = b * b - lambda * lambda / (4 * d * d);
        lambda = Mathf.Sqrt(lambda);

        // Work out d1
        float d1 = a * a - lambda * lambda;
        d1 = Mathf.Sqrt(d1);

        // Setup the bent leg accordingly
        shin.transform.position =
            thigh.transform.position +
            d1 * dvec.normalized -
            lambda * Vector3.Cross(transform.right, dvec.normalized);
    }

    private void OnDrawGizmos()
    {
        // Draw the current grounding point
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(grounding, 0.05f);

        // Draw the raycast test line
        var test = test_start_end;
        Gizmos.DrawLine(test[0], test[1]);

        // Draw the current step point
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(step_point, 0.05f);

        // Draw the thigh
        if (thigh != null && shin != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(thigh.transform.position, shin.transform.position);
        }

        // Draw the shin
        if (shin != null && foot != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(shin.transform.position, foot.transform.position);
        }

        // Draw the foot
        if (foot != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(foot.transform.position, foot.transform.position + foot.transform.forward / 4f);
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(leg))]
    class leg_editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var l = (leg)target;
            base.OnInspectorGUI();
        }
    }
#endif
}