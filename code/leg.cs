using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class leg : MonoBehaviour
{
    public Transform character;  // The character to which these legs belong (stops the legs from stepping on colliders contained within the character itself)
    public Transform foot;       // The foot, with pivot set at the ankle
    public Transform shin;       // The shin, with pivot set at the knee
    public Transform thigh;      // The thigh, with pivot set at the hip
    public leg following;        // The leg I am following, and should be out of phase with

    // Bird knees?
    public bool knees_bend_backward = false;

    Transform step_centre;       // The location/rotation of the foot on Start()
    float thigh_length;          // The distance between hip and knee
    float shin_length;           // The distance between knee and ankle
    float strafe_length;         // The step distance when strafing

    // The initial scales of leg parts
    Vector3 init_foot_scale;
    Vector3 init_thigh_scale;
    Vector3 init_shin_scale;

    // The progress through the current step
    // [0,1] => Foot on ground, going backward
    // [1,2] => Foot in air, going forward
    public float progress { get; private set; }

    // The amount a body should bob up and down because of this leg
    public float body_bob_amt { get { return -Mathf.Sin(progress * Mathf.PI * 2f); } }

    public Vector3 velocity { get; private set; } // The velocity of step_centre, this frame
    Vector3 position_last;                        // The position of step_centre, last frame
    Vector3 ground_normal;                        // The last recorded ground normal

    const float MIN_FOOT_SPEED = 0.2f;
    const float MIN_STEP_SIZE = 0.01f;
    const float MAG_EPSILON = 10e-4f;

    // The source of footstep sounds
    AudioSource footstep_source;
    public float footstep_volume_multiplier = 1f;

    // Is the foot grounded
    bool grounded = false;

    // The length of a step, from step_back to step_front
    float step_length
    {
        get
        {
            // Step length is weighted by the direction we're moving
            float forward_amt = Mathf.Abs(Vector3.Dot(step_direction, step_centre.forward));
            float right_amt = Mathf.Abs(Vector3.Dot(step_direction, step_centre.right));
            float up_amt = Mathf.Abs(Vector3.Dot(step_direction, step_centre.up));

            // Normal step length = leg length
            float normal_length = thigh_length + shin_length;

            // Jump is shorter, so legs flail around belivably when jumping
            float jump_length = shin_length + thigh_length / 2f;

            // Return weighted length
            float ret = normal_length * forward_amt +
                        strafe_length * right_amt +
                        jump_length * up_amt;

            ret *= 1f + Mathf.Min(Mathf.Sqrt(velocity.magnitude / (shin_length + thigh_length)), 0.5f);

            if (ret < MIN_STEP_SIZE) ret = MIN_STEP_SIZE;
            return ret;
        }
    }

    // The direction we're stepping in
    Vector3 step_direction
    {
        get
        {
            // Same as the velocity, default to forward
            if (velocity.magnitude > MAG_EPSILON) return velocity.normalized;
            return step_centre.forward;
        }
    }

    // The front of the step
    Vector3 step_front
    {
        get { return step_centre.position + step_direction * step_length / 2f; }
    }

    // The back of the step
    Vector3 step_back
    {
        get { return step_centre.position - step_direction * step_length / 2f; }
    }

    // Find the grounding nearest the given test point
    Vector3 grounding_point(Vector3 test_point)
    {
        Vector3 test_start = test_point + (shin_length + thigh_length / 2f) * Vector3.up;
        Vector3 test_end = test_point - (shin_length / 2f) * Vector3.up;
        Vector3 delta = test_end - test_start;
        foreach (var h in Physics.RaycastAll(test_start, delta, delta.magnitude))
            if (!h.transform.IsChildOf(character.transform))
            {
                ground_normal = h.normal;
                grounded = true;
                return h.point;
            }

        grounded = false;
        ground_normal = Vector3.up;
        return test_point;
    }

    // Move the foot towards a target point, ensuring
    // it doesn't move too quickly
    void move_foot_towards(Vector3 pos)
    {
        float max_foot_speed = MIN_FOOT_SPEED + velocity.magnitude;
        Vector3 delta = pos - foot.transform.position;
        if (delta.magnitude > max_foot_speed * Time.deltaTime)
            delta = max_foot_speed * Time.deltaTime * delta.normalized;
        foot.transform.position += delta;
    }

    private void Start()
    {
        // Record the foot centre position/orientation
        step_centre = new GameObject("foot_initial").transform;
        step_centre.SetParent(transform);
        step_centre.position = foot.transform.position;
        step_centre.rotation = foot.transform.rotation;

        // Record the thigh/shin lengths
        thigh_length = (thigh.transform.position - shin.transform.position).magnitude;
        shin_length = (shin.transform.position - foot.transform.position).magnitude;

        // Initialize strafe step size
        strafe_length = shin_length;

        // Save initial scales
        init_foot_scale = foot.transform.localScale;
        init_shin_scale = shin.transform.localScale;
        init_thigh_scale = thigh.transform.localScale;

        // Create the footstep sound source
        footstep_source = foot.gameObject.AddComponent<AudioSource>();
        footstep_source.spatialBlend = 1f; // 3D
    }

    bool contact_made_this_step = false;
    void on_foot_contact()
    {
        if (contact_made_this_step) return;
        contact_made_this_step = true;

        bool underwater = false;
        if (foot.transform.position.y < world.SEA_LEVEL)
        {
            underwater = true;
            footstep_source.clip = Resources.Load<AudioClip>("sounds/water_step");
            footstep_source.volume = 0.3f * footstep_volume_multiplier;
        }

        if (!underwater)
        {
            // Re-evaluate the walking sound
            RaycastHit hit;
            var rend = utils.raycast_for_closest<Renderer>(
                new Ray(foot.transform.position + Vector3.up, 
                Vector3.down), out hit);

            Material ground_mat = null;
            if (rend != null) ground_mat = rend.material;

            float vol;
            footstep_source.clip = material_sound.sound(
                material_sound.TYPE.STEP, ground_mat, out vol);
            footstep_source.volume = vol * footstep_volume_multiplier;
        }

        if (grounded && !underwater)
        {
            footstep_source.pitch = Random.Range(0.95f, 1.05f);
            if (footstep_source.isPlaying)
                footstep_source.Stop();
            footstep_source.Play();
        }
        else if (underwater)
        {
            if (!footstep_source.isPlaying)
                footstep_source.Play();
        }
    }

    private void Update()
    {
        // Work out kinematics
        Vector3 delta = step_centre.transform.position - position_last;
        position_last = step_centre.transform.position;
        velocity = delta / Time.deltaTime;

        // Increment progress in step_direction
        if (following == null)
        {
            progress += Vector3.Dot(delta, step_direction) / step_length;
        }
        else
        {
            // Ensure I am out of phase with the leg I am following
            progress = following.progress - 1f;

            // Set the strafe length to be sensible, given the leg I'm following
            strafe_length = (following.step_centre.position - step_centre.position).magnitude;
            following.strafe_length = strafe_length;
        }

        // Progress loops in [0,2]
        progress -= Mathf.Floor(progress / 2f) * 2f;

        if (velocity.magnitude < MAG_EPSILON)
        {
            // Not really moving, reset foot position
            move_foot_towards(grounding_point(step_centre.position));
            solve_leg_positions();
            solve_leg_orientation_and_scale();
            return;
        }

        if (progress < 1f)
        {
            // Foot moving backward on ground
            if (!contact_made_this_step) on_foot_contact();
            Vector3 line_point = step_front * (1 - progress) + step_back * progress;
            move_foot_towards(grounding_point(line_point));
        }
        else
        {
            // Foot moving forward above the ground
            contact_made_this_step = false;
            float fw_prog = progress - 1f;
            Vector3 line_point = step_front * fw_prog + step_back * (1 - fw_prog);
            float amt_above_ground = Mathf.Sin(fw_prog * Mathf.PI) * shin_length / 2f;
            move_foot_towards(grounding_point(line_point) + amt_above_ground * Vector3.up);
        }

        solve_leg_positions();
        solve_leg_orientation_and_scale();
    }

    void solve_leg_orientation_and_scale()
    {
        // Unity uses a left-handed coordinate system for some reason
        // so cross products are left-hand-rule rather than right-hand-rule
        Vector3 knee_to_hip = thigh.transform.position - shin.transform.position;
        Vector3 hip_normal = -Vector3.Cross(knee_to_hip, step_centre.right);
        thigh.transform.rotation = Quaternion.LookRotation(hip_normal, knee_to_hip);
        thigh.transform.localScale = new Vector3(
            init_thigh_scale.x,
            init_thigh_scale.y * knee_to_hip.magnitude / thigh_length,
            init_thigh_scale.z);

        Vector3 foot_to_knee = shin.transform.position - foot.transform.position;
        Vector3 shin_normal = -Vector3.Cross(foot_to_knee, step_centre.right);
        shin.transform.rotation = Quaternion.LookRotation(shin_normal, foot_to_knee);
        shin.transform.localScale = new Vector3(
            init_shin_scale.x,
            init_shin_scale.y * foot_to_knee.magnitude / shin_length,
            init_shin_scale.z);

        if (progress > 1f) // On backswing => foot fixed to shin rotation
            foot.transform.rotation = shin.transform.rotation;
        else // On ground => foot fixed to ground
        {
            Vector3 foot_up = ground_normal - Vector3.Project(ground_normal, step_centre.right);
            if (foot_up.magnitude > MAG_EPSILON)
            {
                Vector3 foot_forward = -Vector3.Cross(foot_up, step_centre.right);
                foot.transform.rotation = Quaternion.LookRotation(foot_forward, foot_up);
            }
            else foot.transform.rotation = step_centre.rotation;
        }
    }

    void solve_leg_positions()
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

        // Asymptotically approach streight leg
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
        if (float.IsNaN(lambda)) return;

        // Setup the bent leg accordingly
        shin.transform.position =
            thigh.transform.position +
            d1 * dvec.normalized -
            lambda * Vector3.Cross(transform.right, dvec.normalized);
    }

    private void OnDrawGizmosSelected()
    {
        if (step_centre != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(step_front, step_back);
            Gizmos.DrawWireSphere(step_back + (step_front - step_back) * progress / 2f, 0.1f);
        }

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
}