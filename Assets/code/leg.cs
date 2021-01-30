//#define LEG_DEBUG
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class leg : MonoBehaviour
{
    const float EPS_MAG = 0.001f;
    const float MIN_FOOT_SPEED = 0.2f;
    const float MIN_STEP_SIZE = 0.01f;
    const float SOUND_DISTANCE = 20f;

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
    public Vector3 ground_normal { get; private set; }
    Transform step_centre;
    Vector3 position_last;
    Vector3 step_direction;
    float step_length;
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
    public bool play_footsteps = true;
    public float min_footstep_pitch = 0.95f;
    public float max_footsetp_pitch = 1.05f;
    public float step_length_boost;
    Vector3 step_front;
    Vector3 step_back;
    Vector3 test_start;
    Vector3 test_end;

    bool grounded { get => (foot_base.position - grounding).magnitude < 0.25f; }

    // The amount a body should bob up and down because of this leg
    public float body_bob_amt { get { return -Mathf.Sin(progress * Mathf.PI * 2f / following_phase); } }

    // Update the various directions/lengths used to determine the step animations
    void update_lengths_and_directions()
    {
        float speed = velocity.magnitude;
        Vector3 step_centre_pos = step_centre.position;
        Vector3 step_centre_fw = step_centre.forward;
        Vector3 step_centre_right = step_centre.right;
        Vector3 step_centre_up = step_centre.up;

        step_direction = speed > EPS_MAG ? velocity / speed : step_centre_fw;

        float fw_amt = Mathf.Abs(Vector3.Dot(step_direction, step_centre_fw));
        float lr_amt = Mathf.Abs(Vector3.Dot(step_direction, step_centre_right));
        float ud_amt = Mathf.Abs(Vector3.Dot(step_direction, step_centre_up));

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

        step_length = step * step_length_boost;
        if (step_length < MIN_STEP_SIZE) step_length = MIN_STEP_SIZE;

        float boost = Mathf.Min(speed / max_boost_at_speed, 1f);
        step_length_boost = min_step_length_boost + boost *
            (max_step_length_boost - min_step_length_boost);

        step_front = step_centre_pos + step_direction * step_length / 2f;
        step_back = step_centre_pos - step_direction * step_length / 2f;
        test_start = step_front + new Vector3(0, test_up_amt, 0);
        test_end = step_front + new Vector3(0, -test_down_amt, 0);
    }

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

        // Desired orientation of thigh
        Vector3 thigh_up = (hip.position - knee.position).normalized;
        Vector3 thigh_forward = Vector3.Cross(transform.right, thigh_up).normalized;

        // Desired orientation of shin
        Vector3 shin_up = (knee.position - ankle.position).normalized;
        Vector3 shin_forward = Vector3.Cross(transform.right, shin_up).normalized;

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

    void set_scales(float hip_length, float shin_length)
    {
        Vector3 ls = hip.localScale;
        ls.y = init_hip_scale * hip_length / thigh_length;
        hip.localScale = ls;

        ls = knee.localScale;
        ls.y = init_knee_scale * shin_length / shin_length;
        knee.localScale = ls;
    }

    void solve_leg()
    {
        // Work out the position of the thigh and shin, given the current foot position
        // according to the following diagram.
        // 
        //                                         a = thigh_length = hip-to-knee distance
        //              hip.position = hip -> .__________. <- knee = shin.transform.position
        //                                     \        /
        //                                      \L     /  
        //                hip-ankle distance = d \    / b = shin_length = knee-to-ankle distance
        //                                        \  /
        //                                         \/. <- ankle = ankle.position
        //
        // point labelled L = the closest point to the knee along the hip-to-ankle line
        //           lambda = the distance between the knee and the point labelled L
        //               d1 = the distance between the hip  and the point labelled L

        float a = thigh_length;
        float b = shin_length;
        Vector3 ankle_pos = ankle.position;
        Vector3 hip_pos = hip.position;
        Vector3 ankle_to_hip = hip_pos - ankle_pos;
        float d = ankle_to_hip.magnitude;
        Vector3 dhat = ankle_to_hip / d; // Direction from ankle to hip
        Vector3 khat = Vector3.Cross(transform.right, dhat); // Direction from L to knee

        if (d > a + b)
        {
            // Foot is further than the maximum extent of the leg, create a streight leg
            knee.position = hip_pos - a * ankle_to_hip / (a + b);

            // Solve orientation and scale
            Quaternion rotation = Quaternion.LookRotation(khat, dhat);
            hip.rotation = rotation;
            knee.rotation = rotation;
            set_scales(a * d / (a + b), b * d / (a + b));

            return;
        }

        // Work out lambda
        float lambda = d * d + b * b - a * a;
        lambda = b * b - lambda * lambda / (4 * d * d);
        lambda = Mathf.Sqrt(lambda);

        // Work out d1/d2
        float d1 = a * a - lambda * lambda;
        d1 = Mathf.Sqrt(d1);
        float d2 = d - d1;

        if (knees_bend_backward) lambda = -lambda;

        // Setup the bent leg accordingly
        Vector3 hip_to_knee = -d1 * dhat + lambda * khat;
        Vector3 ankle_to_knee = hip_to_knee + ankle_to_hip;
        Vector3 new_knee_pos = hip_pos + hip_to_knee;

        if (new_knee_pos.isNaN()) return;
        knee.position = new_knee_pos;

        // Solve orientation and scale
        Vector3 hip_fw = d1 * khat + lambda * dhat;
        Vector3 knee_fw = d2 * khat - lambda * dhat;
        hip_fw -= Vector3.Project(hip_fw, hip_to_knee);
        knee_fw -= Vector3.Project(knee_fw, ankle_to_knee);
        hip.rotation = Quaternion.LookRotation(hip_fw, -hip_to_knee);
        knee.rotation = Quaternion.LookRotation(knee_fw, ankle_to_knee);
        set_scales(a, b);

#if LEG_DEBUG
        debug_hip_forward = hip_fw;
        debug_knee_forward = knee_fw;
        debug_hip_to_knee = hip_to_knee;
        debug_ankle_to_knee = ankle_to_knee;
#endif

    }

#if LEG_DEBUG
    Vector3 debug_hip_to_knee;
    Vector3 debug_ankle_to_knee;
    Vector3 debug_hip_forward;
    Vector3 debug_knee_forward;
#endif

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

    void play_contact_sounds()
    {
        bool underwater = foot_base.transform.position.y < world.SEA_LEVEL &&
                          foot_base.transform.position.y > world.UNDERGROUND_ROOF;
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
                    new Ray(test_start, Vector3.down), out hit,
                    max_distance: test_up_amt * 2f);

                Material ground_mat = null;
                if (rend != null) ground_mat = rend.sharedMaterial;

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

    RaycastHit find_grounding(out bool valid)
    {
        // Find the grounding point
        Vector3 delta = test_end - test_start;

        // Attempt to raycast all objects
        if (Physics.Raycast(test_start, delta, out RaycastHit hit, delta.magnitude))
        {
            // The object found was a child of this character, fall back to
            // an everything raycast until we find a non-character hit
            if (hit.transform.IsChildOf(character))
            {
                foreach (var h in Physics.RaycastAll(test_start, delta, delta.magnitude))
                {
                    if (h.transform.IsChildOf(character)) continue;
                    valid = true;
                    return h;
                }
            }
            else
            {
                valid = true;
                return hit;
            }
        }

        valid = false;
        return default;
    }

    void make_contact(Vector3 test_point)
    {
        var h = find_grounding(out bool found);

        if (found)
        {
            grounding = h.point;
            ground_normal = h.normal;
            contact_made_this_step = true;
        }
        else
            ground_normal = Vector3.up;

        if (play_footsteps)
            play_contact_sounds();
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

        update_lengths_and_directions();

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
            // Essentially not moving, stay as we are
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
#if LEG_DEBUG
        Gizmos.color = Color.red;
        Gizmos.DrawLine(hip.position, hip.position + debug_hip_to_knee);
        Gizmos.DrawLine(ankle.position, ankle.position + debug_ankle_to_knee);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(hip.position, hip.position + debug_hip_forward.normalized / 10f);
        Gizmos.DrawLine(knee.position, knee.position + debug_knee_forward.normalized / 10f);
#else
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
        Gizmos.DrawLine(grounding, grounding + ground_normal);
#endif
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

                string debug = "Progress " + l.progress + "\n" +
                               "Speed " + l.velocity.magnitude + "\n" +
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
                    foreach (Transform t in l.GetComponentsInChildren<Transform>())
                    {
                        if (t.name.Contains("ankle")) l.ankle = t;
                        else if (t.name.Contains("knee")) l.knee = t;
                        else if (t.name.Contains("hip")) l.hip = t;
                        else if (t.name.Contains("foot_base")) l.foot_base = t;
                        else if (t.name.Contains("upper")) l.hip = t;
                        else if (t.name.Contains("lower")) l.knee = t;
                        else if (t.name.Contains("foot")) l.ankle = t;
                    }

                    var c = l.GetComponentInParent<character>();
                    if (c != null) l.character = c.transform;
                }
            }
        }
    }
#endif

}
