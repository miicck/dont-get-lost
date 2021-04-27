using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class body : MonoBehaviour
{
    public float bob_amplitude = 0.1f;
    public float lean_into_run_amount = 20f;
    public float lean_with_arms_amount = 45f;
    public float lean_lerp_speed = 5f;
    public float lean_velocity_scale = 5f;
    public Transform character;
    public Transform head;
    public float head_bob_amt = 0f;
    public float head_lean_inherit_amt = 0f;

    leg[] legs;
    arm[] arms;
    Vector3 init_local_pos;
    float lean = 0;

    new Transform transform;

    void Start()
    {
        // Replace my transform with one alligned to the player
        transform = new GameObject("body").transform;
        transform.rotation = character.rotation;
        transform.position = base.transform.position;
        transform.SetParent(base.transform.parent);
        base.transform.SetParent(transform);

        legs = character.GetComponentsInChildren<leg>();
        arms = character.GetComponentsInChildren<arm>();
        init_local_pos = transform.localPosition;
    }

    /// <summary> Returns the current bob amount, based on info from legs. </summary>
    float bob_amt()
    {
        if (legs.Length > 0) return legs[0].body_bob_amt;
        return Mathf.Sin(Mathf.PI * Time.realtimeSinceStartup);
    }

    private void Update()
    {
        Quaternion saved_head_rotation = Quaternion.identity;
        float saved_head_y = 0;
        if (head != null)
        {
            saved_head_rotation = head.transform.rotation;
            saved_head_y = head.transform.position.y;
        }

        // Bob up/down in time with leg
        transform.localPosition = init_local_pos + Vector3.up * bob_amt() * bob_amplitude;

        // Work out how much we want to lean
        float lean_target = 0f;
        float vdot = Vector3.Dot(legs[0].velocity, transform.forward);
        lean_target += lean_into_run_amount * utils.tanh(vdot / lean_velocity_scale);
        float av_in_front = 0;
        foreach (var a in arms) av_in_front += a.in_front_amount;
        av_in_front /= arms.Length;
        lean_target += lean_with_arms_amount * av_in_front;
        if (float.IsNaN(lean_target)) lean_target = lean;

        // Lerp the actual lean amount
        lean = Mathf.Lerp(lean, lean_target, Time.deltaTime * lean_lerp_speed);
        if (float.IsNaN(lean)) lean = 0;

        // Apply the lean
        transform.localRotation = Quaternion.Euler(
            lean,
            transform.localRotation.eulerAngles.y,
            transform.localRotation.eulerAngles.z);

        if (head != null)
        {
            head.transform.rotation = Quaternion.Lerp(
                saved_head_rotation, head.transform.rotation, head_lean_inherit_amt);

            head.transform.position +=
                Vector3.up * (saved_head_y - head.transform.position.y)
                * (1 - head_bob_amt);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            base.transform.position,
            base.transform.position + Vector3.up * bob_amplitude);
    }
}
