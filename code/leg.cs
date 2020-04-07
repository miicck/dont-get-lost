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

    // Should the knees bend backward
    public bool knees_bend_backward = false;

    Vector3 grounding_next;      // Point which the foot next makes contact with the ground 
    Vector3 grounding_last;      // Point which the foot last made contact with the ground
    Vector3 step_point;          // Current point along the line from the previous grounding to next grounding
    Vector3 position_last;       // The last position of leg.transform (used to calculate transform_velocity)
    Vector3 transform_velocity;  // The current velocity of leg.transform

    float thigh_length;          // The length of the thigh (calculated in start())
    float shin_length;           // The length of the shin (calculated in start())

    // The length of a step
    float step_length
    {
        get
        {
            const float MIN_STEP = 0.01f;
            const float MAX_STEP = 1f;
            float scale = (thigh_length + shin_length);
            return Mathf.Clamp(Mathf.Sqrt(transform_velocity.magnitude), MIN_STEP, MAX_STEP) * scale;
        }
    }

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

    // A direction perpendicular to the step
    // (assumes step_direction is not parallel/antiparallel to Vector3.up)
    Vector3 step_tangent
    {
        get
        {
            return Vector3.Cross(step_direction, Vector3.up);
        }
    }

    // The centre of the current step
    Vector3 stride_centre { get { return transform.position; } }

    Vector3 test_centre
    {
        get
        {
            // We look for the next grounding point 1.5f step lengths ahead. This is
            // it exactly far ahead enough so that by the time the feet catch up and hit the point, 
            // they will be 0.5f step lengths in front (exactly as they should be).
            return stride_centre + step_direction * step_length * 1.5f;
        }
    }

    // The two points between which to raycast for solid ground
    Vector3[] test_start_end
    {
        get
        {
            Vector3 test_dir = -Vector3.Cross(step_tangent, step_direction);

            return new Vector3[]
            {
                test_centre + test_dir,
                test_centre - test_dir
            };
        }
    }

    void new_grounding(Vector3 new_grounding)
    {
        // Don't set gronding point too far up
        float max_y = shin_length + thigh_length / 2f;
        if ((new_grounding - stride_centre).y > max_y)
            new_grounding.y = stride_centre.y + max_y;

        grounding_last = grounding_next;
        grounding_next = new_grounding;
    }

    // Update the grounding point to the next grounding point (i.e start taking a step)
    void generate_next_grounding()
    {
        var test = test_start_end;
        Vector3 delta = test[1] - test[0];

        // Look for solid ground between test[0] and test[1]
        foreach (var h in Physics.RaycastAll(test[0], delta.normalized, delta.magnitude))
        {
            if (h.collider.transform.IsChildOf(character))
                continue;

            new_grounding(h.point);
            return;
        }

        // None found, just set to midway
        new_grounding((test[0] + test[1]) / 2f);
    }

    private void Start()
    {
        // Calculate thigh/shin lengths
        thigh_length = (thigh.transform.position - shin.transform.position).magnitude;
        shin_length = (shin.transform.position - foot.transform.position).magnitude;

        // Initialize step control points
        grounding_next = stride_centre;
        step_point = grounding_next;
        position_last = transform.position;
    }

    bool need_new_grounding()
    {
        // If the next grounding point is > 0.5 steps behind, this 
        // step has finished and we need a new grounding point
        Vector3 delta = grounding_next - stride_centre;
        if (Vector3.Dot(delta, step_direction) < -0.5f * step_length) return true;

        // If the next grounding point is > 0.5 steps in the perpendicular direction
        // the grounding point is out of sensible range and needs an update
        if (Mathf.Abs(Vector3.Dot(delta, step_tangent)) > 0.5f * step_length) return true;

        // This grounding is OK for now
        return false;
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
            float ahead_amt = Vector3.Dot(grounding_next - lead_by.grounding_next, step_direction) / step_length;

            if (Mathf.Abs(ahead_amt) < 0.9f)
            {
                // Legs too close to in phase
                if (ahead_amt > 0)
                    grounding_next += step_direction * step_length * (1f - ahead_amt);
                else
                    grounding_next -= step_direction * step_length * (1f + ahead_amt);
            }
        }

        // Generate new grounding point if needed
        if (need_new_grounding())
            generate_next_grounding();

        // Move the step_point along the line towards the grounding
        Vector3 disp = grounding_next - step_point;
        Vector3 move = disp;
        if (move.magnitude > max_foot_speed * Time.deltaTime)
            move = move.normalized * max_foot_speed * Time.deltaTime;
        step_point += move;

        // Work out how far through a step we are and set the foot raise amount accordingly.
        float step_progress = (step_point - grounding_last).magnitude / (grounding_next - grounding_last).magnitude;
        float raise_amt = Mathf.Max(0, Mathf.Sin(Mathf.PI * step_progress));

        // The foot is only raised if it is moving relative to the ground.
        raise_amt *= (move.magnitude / Time.deltaTime) / max_foot_speed;

        // Set the foot position the apropriate amount above step_point
        set_foot_position(step_point + Vector3.up * raise_amt * shin_length / 2f);

        // Work out the thigh/shin positions
        //shin.transform.position = (thigh.transform.position + foot.transform.position) / 2f;
        solve_leg();
    }

    void set_foot_position(Vector3 pos)
    {
        Vector3 hip_disp = pos - thigh.transform.position;
        float max_leg_length = 1.25f * (thigh_length + shin_length);
        if (hip_disp.magnitude > max_leg_length)
            hip_disp = hip_disp.normalized * max_leg_length;

        foot.transform.position = thigh.transform.position + hip_disp;
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

        if (knees_bend_backward) lambda = -lambda;

        // Setup the bent leg accordingly
        shin.transform.position =
            thigh.transform.position +
            d1 * dvec.normalized -
            lambda * Vector3.Cross(transform.right, dvec.normalized);
    }

    private void OnDrawGizmos()
    {
        // Draw the grounding points
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(grounding_last, 0.05f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(grounding_next, 0.05f);

        // Draw the raycast test line
        var test = test_start_end;
        Gizmos.DrawLine(test[0], test[1]);

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