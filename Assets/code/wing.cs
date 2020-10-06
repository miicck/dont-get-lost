using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class wing : MonoBehaviour
{
    public Transform character;

    public float max_angle = 60f;
    public float min_angle = 0f;
    public float leg_flap_inherit = 0.25f;
    public float flutter_flap_time = 0.15f;
    public float min_time_between_flutter = 5f;
    public float max_time_between_flutter = 30f;

    leg[] legs;
    leg closest_leg;

    void Start()
    {
        legs = character.GetComponentsInChildren<leg>();
        closest_leg = utils.find_to_min(legs, (l) => (l.transform.position - transform.position).magnitude);
        unflutter();
    }

    bool fluttering = false;
    void flutter() { fluttering = true; Invoke("unflutter", 1f); }
    void unflutter() { fluttering = false; Invoke("flutter", Random.Range(min_time_between_flutter, max_time_between_flutter)); }

    float random_flap
    {
        get
        {
            if (!fluttering) return 0f;
            return (1f + Mathf.Sin(Time.realtimeSinceStartup * Mathf.PI * 2 / flutter_flap_time)) / 2f;
        }
    }

    float wing_angle
    {
        get
        {
            float leg_amt = (closest_leg.body_bob_amt + 1f) / 2f;

            float total_amt = leg_amt * leg_flap_inherit + random_flap;
            total_amt = Mathf.Clamp(total_amt, 0, 1f);
            return min_angle + (max_angle - min_angle) * total_amt;
        }
    }

    void Update()
    {
        Vector3 angles = transform.localRotation.eulerAngles;
        angles.z = wing_angle;
        transform.localRotation = Quaternion.Euler(angles);
    }
}
