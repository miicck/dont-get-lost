using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class body : MonoBehaviour
{
    public float bob_amplitude = 0.1f;
    public Transform character;
    leg[] legs;

    Vector3 init_local_pos;
    void Start()
    {
        legs = character.GetComponentsInChildren<leg>();
        init_local_pos = transform.localPosition;
    }

    float bob_amt()
    {
        if (legs.Length > 0) return legs[0].body_bob_amt;
        return Mathf.Sin(Mathf.PI * Time.realtimeSinceStartup);
    }

    private void Update()
    {
        transform.localPosition = init_local_pos + Vector3.up * bob_amt() * bob_amplitude;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * bob_amplitude);
    }
}
