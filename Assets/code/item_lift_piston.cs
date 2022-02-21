using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_lift_piston : MonoBehaviour, INonBlueprintable
{
    public Transform bottom;
    public Transform top;
    public float lift_time = 1f;

    item_input[] inputs;
    item_output[] outputs;
    int input_index = 0;
    float speed = 1f;
    int output_index = 0;

    item item;

    private void Start()
    {
        var bm = GetComponentInParent<building_material>();
        inputs = bm.GetComponentsInChildren<item_input>();
        outputs = bm.GetComponentsInChildren<item_output>();
        speed = (top.position - bottom.position).magnitude / lift_time;
    }

    private void Update()
    {
        if (item == null)
        {
            // Going down
            if (utils.move_towards(transform, bottom.position, Time.deltaTime * speed))
            {
                var next_input = inputs[input_index];
                input_index = (input_index + 1) % inputs.Length;

                // Arrived at bottom, attempt to pick up item
                if (next_input.item_count > 0)
                {
                    item = next_input.release_next_item();
                    item.transform.SetParent(transform);
                    item.transform.position = transform.position;
                    item.transform.up = Vector3.up;
                }
            }
        }
        else
        {
            // Going up
            if (utils.move_towards(transform, top.position, Time.deltaTime * speed))
            {
                // Cycle outputs until we find one that is connected
                // If none are connected, we just use the same output every time
                item_output next_output = null;
                for (int i = 0; i < outputs.Length; ++i)
                {
                    next_output = outputs[output_index];
                    output_index = (output_index + 1) % outputs.Length;
                    if (next_output.output_count > 0)
                        break;
                }

                // Arrived at top, drop off item
                next_output.add_item(item);
                item.transform.SetParent(null);
                item.transform.localScale = Vector3.one * item.logistics_scale;
                item = null;
            }
        }
    }
}
