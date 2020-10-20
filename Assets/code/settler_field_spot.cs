using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_field_spot : MonoBehaviour, INonBlueprintable, INonEquipable
{
    public bool grown => progress >= 1f;
    public GameObject to_grow;
    public product[] products => GetComponents<product>();
    public float growth_time = 30f;

    GameObject grown_object;
    float progress = 0f;

    private void Start()
    {
        grown_object = to_grow.inst();
        grown_object.transform.SetParent(transform);
        grown_object.transform.localPosition = Vector3.zero;
        grown_object.transform.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        progress += Time.deltaTime / growth_time;
        grown_object.transform.localScale = Vector3.one * Mathf.Clamp(progress, 0f, 1f);
    }

    public void harvest()
    {
        progress = 0;
    }

    public void tend()
    {
        progress += 10f / growth_time;
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.Lerp(Color.red, Color.green, Mathf.Clamp(progress, 0f, 1f));
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 0.05f, 1f));
    }
}
