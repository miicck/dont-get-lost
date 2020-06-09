using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class leg : MonoBehaviour
{
    const float EPS_MAG = 0.001f;
    const float MIN_FOOT_SPEED = 0.2f;
    const float MIN_STEP_SIZE = 0.01f;

    public Transform character;
    public Transform hip;
    public Transform knee;
    public Transform ankle;
    public Transform foot_base;
    public bool knees_bend_backward;
    public leg following;
    public float following_phase = 1f;
    public float min_step_length_boost = 0.1f;
    public float max_step_length_boost = 1f;
    public float max_boost_at_speed = player.BASE_SPEED;
    public float max_leg_stretch = 1.5f;

    public Vector3 velocity { get; private set; }
    public float progress { get; private set; }
    Transform step_centre;
    Vector3 position_last;
    bool contact_made_this_step;
    Vector3 grounding;
    float thigh_length;
    float shin_length;
    float ankle_length;
    float init_hip_scale;
    float init_knee_scale;
    float test_up_amt;
    float test_down_amt;
    AudioSource footstep_source;
    public AudioClip custom_footstep_sound;
    public float footstep_volume_multiplier = 1f;
    public float min_footstep_pitch = 0.95f;
    public float max_footsetp_pitch = 1.05f;

    public float step_length_boost
    {
        get
        {
            float boost = velocity.magnitude / max_boost_at_speed;
            if (boost > 1f) boost = 1f;
            return min_step_length_boost + boost *
                (max_step_length_boost - min_step_length_boost);
        }
    }

    float step_length
    {
        get
        {
            float fw_amt = Mathf.Abs(Vector3.Dot(step_direction, step_centre.forward));
            float lr_amt = Mathf.Abs(Vector3.Dot(step_direction, step_centre.right));
            float ud_amt = Mathf.Abs(Vector3.Dot(step_direction, step_centre.up));

            float tot = fw_amt + lr_amt + ud_amt;
            fw_amt /= tot;
            lr_amt /= tot;
            ud_amt /= tot;

            float fw_length = shin_length + thigh_length + ankle_length;
            float lr_length = shin_length;
            float ud_length = shin_length + ankle_length + thigh_length / 2f;

            float step = fw_amt * fw_length +
                         lr_amt * lr_length +
                         ud_amt * ud_length;

            var ret = step * step_length_boost;
            if (ret < MIN_STEP_SIZE) ret = MIN_STEP_SIZE;
            return ret;
        }
    }

    bool grounded { get => (foot_base.position - grounding).magnitude < 0.25f; }

    Vector3 step_direction
    {
        get => velocity.magnitude > EPS_MAG ? velocity.normalized : step_centre.forward;
    }

    // The amount a body should bob up and down because of this leg
    public float body_bob_amt { get { return -Mathf.Sin(progress * Mathf.PI * 2f / following_phase); } }

    // Desired orientation of thigh
    Vector3 thigh_up { get => (hip.position - knee.position).normalized; }
    Vector3 thigh_forward { get => Vector3.Cross(transform.right, thigh_up).normalized; }

    // Desired orientation of shin
    Vector3 shin_up { get => (knee.position - ankle.position).normalized; }
    Vector3 shin_forward { get => Vector3.Cross(transform.right, shin_up).normalized; }

    Vector3 step_front { get => step_centre.position + step_direction * step_length / 2f; }
    Vector3 step_back { get => step_centre.position - step_direction * step_length / 2f; }

    Vector3 test_start { get => step_front + Vector3.up * test_up_amt; }
    Vector3 test_end { get => step_front - Vector3.up * test_down_amt; }

    private void Start()
    {
        // Get the hip-to-ankle vector
        Vector3 whole_leg = ankle.position - hip.position;

        // Work out the direction the knee should bend in
        Vector3 knee_bend_dir = knee.position - hip.position;
        knee_bend_dir -= Vector3.Project(knee_bend_dir, whole_leg);
        if (knee_bend_dir.magnitude < EPS_MAG)
        {
            string err = "Knee must be sligtly in front of the hip -> ankle line!";
            err += " (" + name + ")";
            throw new System.Exception(err);
        }
        knee_bend_dir.Normalize();

        // Work out the right direction (perpendicular to the knee 
        // bend direction) and the forward direction (perpendicular 
        // to right/up).
        Vector3 right = Vector3.Cross(knee_bend_dir, whole_leg).normalized;
        Vector3 forward = Vector3.Cross(right, Vector3.up);
        utils.align_axes(transform, Quaternion.LookRotation(forward, Vector3.up));

        // Reorient the hip so that hip.down points to the knee
        var new_hip = new GameObject("hip").transform;
        new_hip.position = hip.position;
        new_hip.SetParent(transform);
        new_hip.rotation = Quaternion.LookRotation(thigh_forward, thigh_up);
        hip.SetParent(new_hip);
        hip = new_hip;

        // Reorient the knee so that knee.down points to the ankle
        var new_knee = new GameObject("knee").transform;
        new_knee.position = knee.position;
        new_knee.SetParent(transform);
        new_knee.rotation = Quaternion.LookRotation(shin_forward, shin_up);
        knee.SetParent(new_knee);
        knee = new_knee;

        // Work out the lengths of the various parts of the leg
        thigh_length = (hip.position - knee.position).magnitude;
        shin_length = (knee.position - ankle.position).magnitude;
        ankle_length = (ankle.position - foot_base.position).magnitude;

        // Record the initial foot position
        step_centre = new GameObject("step_centre").transform;
        step_centre.position = foot_base.position;
        step_centre.rotation = transform.rotation;
        step_centre.SetParent(transform);

        // Initialize kineamtics
        position_last = step_centre.position;
        velocity = Vector3.zero;
        progress = 0;

        // Ensure heirarchy is correct
        foot_base.transform.SetParent(ankle);
        ankle.transform.SetParent(transform);
        knee.transform.SetParent(transform);
        hip.transform.SetParent(transform);

        // Remember the intial scales
        init_hip_scale = hip.transform.localScale.y;
        init_knee_scale = knee.transform.localScale.y;

        test_up_amt = Mathf.Max(
            shin_length,
            hip.position.y - foot_base.position.y,
            knee.position.y - foot_base.position.y,
            ankle.position.y - foot_base.position.y
        );

        test_down_amt = test_up_amt;

        footstep_source = new GameObject("footstep_source").AddComponent<AudioSource>();
        footstep_source.transform.SetParent(foot_base.transform);
        footstep_source.transform.localPosition = Vector3.zero;
        footstep_source.spatialBlend = 1f; // 3D
    }

    void solve_orientation_and_scale()
    {
        hip.rotation = Quaternion.LookRotation(thigh_forward, thigh_up);
        float new_thigh_lenth = (hip.position - knee.position).magnitude;
        Vector3 ls = hip.transform.localScale;
        ls.y = init_hip_scale * new_thigh_lenth / thigh_length;
        hip.transform.localScale = ls;

        knee.rotation = Quaternion.LookRotation(shin_forward, shin_up);
        float new_shin_length = (knee.position - ankle.position).magnitude;
        ls = knee.transform.localScale;
        ls.y = init_knee_scale * new_shin_length / shin_length;
        knee.transform.localScale = ls;
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
        Vector3 dvec = ankle.position - hip.position;
        float d = dvec.magnitude;

        // Asymptotically approach streight leg
        if (d > a + b)
        {
            // Foot is further than the maximum extent of the leg, create a streight leg
            knee.position = hip.position + a * dvec / (a + b);
            solve_orientation_and_scale();
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
        Vector3 new_knee_pos =
            hip.position +
            d1 * dvec.normalized -
            lambda * Vector3.Cross(transform.right, dvec.normalized);

        if (new_knee_pos.isNaN()) return;
        knee.position = new_knee_pos;
        solve_orientation_and_scale();
    }

    void move_foot_towards(Vector3 position)
    {
        // To move the foot, we actually move the ankle so 
        // that the foot is in the given location.
        Vector3 foot_to_ankle = ankle.position - foot_base.position;
        Vector3 delta = position + foot_to_ankle - ankle.position;

        // Ensure we don't move the foot too fast
        float max_foot_speed = MIN_FOOT_SPEED + velocity.magnitude;
        if (delta.magnitude > max_foot_speed * Time.deltaTime)
            delta = delta.normalized * max_foot_speed * Time.deltaTime;

        // Check we don't over-stretch the leg
        Vector3 new_pos = ankle.position + delta;
        float new_leg_length = (new_pos - hip.position).magnitude;
        if (new_leg_length / (shin_length + thigh_length) > max_leg_stretch)
            return;

        ankle.position += delta;
    }

    void make_contact(Vector3 test_point)
    {
        // Find the grounding point
        Vector3 delta = test_end - test_start;
        foreach (var h in Physics.RaycastAll(test_start, delta, delta.magnitude))
        {
            if (h.transform.IsChildOf(character)) continue;
            grounding = h.point;
            contact_made_this_step = true;
            break;
        }

        bool underwater = foot_base.transform.position.y < world.SEA_LEVEL;
        if (underwater)
        {
            footstep_source.clip = Resources.Load<AudioClip>("sounds/water_step");
            footstep_source.volume = 0.3f * footstep_volume_multiplier;
        }
        else
        {
            if (custom_footstep_sound != null)
            {
                // Use custom footstep sound
                footstep_source.clip = custom_footstep_sound;
                footstep_source.volume = footstep_volume_multiplier;
            }
            else
            {
                // Re-evaluate the walking sound based on ground type
                RaycastHit hit;
                var rend = utils.raycast_for_closest<Renderer>(
                    new Ray(foot_base.position + Vector3.up,
                    Vector3.down), out hit);

                Material ground_mat = null;
                if (rend != null) ground_mat = rend.material;

                float vol;
                footstep_source.clip = material_sound.sound(
                    material_sound.TYPE.STEP, ground_mat, out vol);
                footstep_source.volume = vol * footstep_volume_multiplier;
            }
        }

        if (grounded && !underwater)
        {
            footstep_source.pitch = Random.Range(min_footstep_pitch, max_footsetp_pitch);
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

    float flick_amount
    {
        get
        {
            if (progress < 1.5f) return 0f;
            return 0.25f * step_length * (progress - 1.5f) / 0.5f;
        }
    }

    private void Update()
    {
        Vector3 delta = step_centre.position - position_last;
        position_last = step_centre.position;
        velocity = delta / Time.deltaTime;

        if (following == null)
            // Increment progress in step_direction
            progress += Vector3.Dot(delta, step_direction) / step_length;
        else
            // Ensure I'm out of phase with the leg I'm following
            progress = following.progress - following_phase;

        // Progress loops in [0,2]
        progress -= Mathf.Floor(progress / 2f) * 2f;

        if (velocity.magnitude < EPS_MAG)
        {
            // Essentially not moving, return to default position
            move_foot_towards(step_centre.position);
            solve_leg();
            return;
        }

        if (progress < 1f)
        {
            // Foot moving backwards on the ground
            if (!contact_made_this_step) make_contact(step_front);

            if (grounded)
            {
                move_foot_towards(grounding + Vector3.up * flick_amount);
            }
            else
            {
                float bw_progresss = progress;
                Vector3 line_point = step_back * bw_progresss + step_front * (1 - bw_progresss);
                move_foot_towards(line_point + Vector3.up * flick_amount);
            }
        }
        else
        {
            // Foot moving forwards in the air
            contact_made_this_step = false;
            float fw_prog = progress - 1f;

            Vector3 line_point = step_front * fw_prog + step_back * (1 - fw_prog);
            float amt_above_ground = Mathf.Sin(fw_prog * Mathf.PI) * shin_length / 2f;
            move_foot_towards(line_point + amt_above_ground * Vector3.up);
        }

        solve_leg();
    }

    private void OnDrawGizmos()
    {
        if (hip == null || knee == null || ankle == null || foot_base == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(hip.position, knee.position);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(knee.position, ankle.position);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(ankle.position, foot_base.position);

        if (step_centre == null) return;

        Gizmos.DrawLine(
            step_centre.position + step_length * step_direction / 2f,
            step_centre.position - step_length * step_direction / 2f);

        Gizmos.DrawLine(test_start, test_end);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(grounding, 0.05f);
    }

#if UNITY_EDITOR
    [UnityEditor.CanEditMultipleObjects()]
    [UnityEditor.CustomEditor(typeof(leg))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (Application.isPlaying)
            {
                var l = (leg)target;

                float stretch_amt = (l.hip.position - l.knee.position).magnitude;
                stretch_amt += (l.knee.position - l.ankle.position).magnitude;
                stretch_amt /= (l.thigh_length + l.shin_length);

                string debug = "Speed " + l.velocity.magnitude + "\n" +
                               "Step boost " + l.step_length_boost + "\n" +
                               "Step length " + l.step_length + "\n" +
                               "Stretch " + stretch_amt + "\n";

                UnityEditor.EditorGUILayout.TextArea(debug);
                UnityEditor.EditorUtility.SetDirty(target); // Update every frame
            }
            else
            {
                if (UnityEditor.EditorGUILayout.Toggle("Auto setup", false))
                {
                    var l = (leg)target;
                    foreach (Transform t in l.transform)
                    {
                        if (t.name.Contains("ankle")) l.ankle = t;
                        else if (t.name.Contains("knee")) l.knee = t;
                        else if (t.name.Contains("hip")) l.hip = t;
                        else if (t.name.Contains("foot_base")) l.foot_base = t;
                    }
                }
            }
        }
    }
#endif

}
