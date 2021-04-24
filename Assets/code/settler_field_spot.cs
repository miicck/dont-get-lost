using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_field_spot : networked, IPlayerInteractable
{
    networked_variables.net_float progress;
    float progress_scale => progress.value * (1f - min_scale) + min_scale;

    public override void on_init_network_variables()
    {
        progress = new networked_variables.net_float(
            resolution: 0.05f,
            min_value: 0f,
            max_value: 1f);

        progress.on_change = () =>
        {
            // If progress has been reduced, that means we've been harvested
            if (progress_scale < grown_object.transform.localScale.x)
            {
                var field = GetComponentInParent<settler_field>();
                if (field != null)
                    foreach (var p in products)
                        p.create_in_node(field.output, true);
            }

            grown_object.transform.localScale = Vector3.one * progress_scale;
        };
    }

    public bool grown => progress.value >= 1f;
    public GameObject to_grow;
    public product[] products => GetComponents<product>();
    public float growth_time = 30f;
    public float min_scale = 0.2f;


    GameObject grown_object
    {
        get
        {
            if (_grown_object == null)
            {
                // Create the grown object
                _grown_object = to_grow.inst();
                _grown_object.transform.SetParent(transform);
                _grown_object.transform.localPosition = Vector3.zero;
                _grown_object.transform.localRotation = Quaternion.identity;
            }
            return _grown_object;
        }
    }
    GameObject _grown_object;

    private void Update()
    {
        if (!has_authority) return;
        progress.value += Time.deltaTime / growth_time;
    }

    public void harvest()
    {
        progress.value = 0f;
    }

    public void tend()
    {
        progress.value += 10f / growth_time;
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.Lerp(Color.red, Color.green, progress.value);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 0.05f, 1f));
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public player_interaction[] player_interactions()
    {
        return new player_interaction[]
        {
            new player_inspectable(transform)
            {
                text = () => Mathf.Round(progress.value * 100f) + "% grown"
            }
        };
    }
}