using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class wing : MonoBehaviour
{
    public Transform character;
    public Transform wing_tip;

    public bool is_flying = false;

    // Variables pertaining to wing movement when flying
    public float max_angle = 60f;
    public float min_angle = 0f;
    public float flap_period = 0.25f;

    // Variables pertaining to wing movement when on the ground
    public Vector3 fold_back_angles_min = Vector3.zero;
    public Vector3 fold_back_angles_max = Vector3.zero;
    public float leg_flap_inherit = 0.25f;
    public float flutter_flap_time = 0.15f;
    public float min_time_between_flutter = 5f;
    public float max_time_between_flutter = 30f;

    leg[] legs;
    leg closest_leg;

    bool is_left_wing => Vector3.Dot(transform.position - character.transform.position, character.transform.right) < 0;

    Quaternion max_flap_rotation
    {
        get => Quaternion.AngleAxis(is_left_wing ? -max_angle : max_angle, character.transform.forward);
    }

    Quaternion min_flap_rotation
    {
        get => Quaternion.AngleAxis(is_left_wing ? -min_angle : min_angle, character.transform.forward);
    }

    Quaternion folded_rotation_min
    {
        get => Quaternion.AngleAxis(fold_back_angles_min.x, character.transform.right) *
               Quaternion.AngleAxis(is_left_wing ? fold_back_angles_min.z : -fold_back_angles_min.z, character.transform.forward) *
               Quaternion.AngleAxis(is_left_wing ? fold_back_angles_min.y : -fold_back_angles_min.y, character.transform.up);
    }

    Quaternion folded_rotation_max
    {
        get => Quaternion.AngleAxis(fold_back_angles_max.x, character.transform.right) *
               Quaternion.AngleAxis(is_left_wing ? fold_back_angles_max.z : -fold_back_angles_max.z, character.transform.forward) *
               Quaternion.AngleAxis(is_left_wing ? fold_back_angles_max.y : -fold_back_angles_max.y, character.transform.up);
    }

    Quaternion initial_local_rotation;

    void Start()
    {
        legs = character.GetComponentsInChildren<leg>();
        closest_leg = utils.find_to_min(legs, (l) => (l.transform.position - transform.position).magnitude);
        initial_local_rotation = transform.localRotation;
        unflutter();
    }

    bool fluttering = false;
    void flutter() { fluttering = true; Invoke("unflutter", 1f); }
    void unflutter() { fluttering = false; Invoke("flutter", Random.Range(min_time_between_flutter, max_time_between_flutter)); }

    float flap_amount
    {
        get
        {
            if (is_flying)
                return (1f + Mathf.Sin(Time.realtimeSinceStartup * Mathf.PI * 2 / flap_period)) / 2f;

            // Inherit rotation from leg
            float amt = leg_flap_inherit * (closest_leg.body_bob_amt + 1f) / 2f;

            if (fluttering)
            {
                // Add a flutter
                amt += (1f + Mathf.Sin(Time.realtimeSinceStartup * Mathf.PI * 2 / flutter_flap_time)) / 2f;
            }

            return Mathf.Clamp(amt, 0, 1f);
        }
    }

    void Update()
    {
        transform.localRotation = initial_local_rotation;
        Quaternion rotation_mod = is_flying ?
            Quaternion.Lerp(min_flap_rotation, max_flap_rotation, flap_amount) :
            Quaternion.Lerp(folded_rotation_min, folded_rotation_max, flap_amount);
        transform.rotation = rotation_mod * transform.rotation;
    }

    private void OnDrawGizmosSelected()
    {
        foreach (var m in GetComponentsInChildren<MeshFilter>())
        {
            Vector3 mdelta = m.transform.position - transform.position;

            Gizmos.color = new Color(1, 0, 0, 0.2f);
            Gizmos.DrawMesh(m.sharedMesh, transform.position + min_flap_rotation * mdelta, min_flap_rotation * m.transform.rotation, m.transform.lossyScale);
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawMesh(m.sharedMesh, transform.position + max_flap_rotation * mdelta, max_flap_rotation * m.transform.rotation, m.transform.lossyScale);


            Gizmos.color = new Color(1, 0, 1, 0.2f);
            Gizmos.DrawMesh(m.sharedMesh, transform.position + folded_rotation_min * mdelta, folded_rotation_min * m.transform.rotation, m.transform.lossyScale);
            Gizmos.color = new Color(0, 1, 1, 0.2f);
            Gizmos.DrawMesh(m.sharedMesh, transform.position + folded_rotation_max * mdelta, folded_rotation_max * m.transform.rotation, m.transform.lossyScale);
        }
    }
}