using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class filter_splitter : MonoBehaviour
{
    public chest read_split_from;
    public item_input input;
    public item_output filtered_output;
    public item_output other_output;
    public Transform item_filtering_spot;
    public Transform item_processing_spot;
    public Transform scales;
    public Transform scales_rotated;
    public float rotation_period = 1f;

    item item_filtering
    {
        get => _item_filtering;
        set
        {
            _item_filtering = value;

            // Remove previous displayed filterer
            foreach (Transform t in item_filtering_spot)
                Destroy(t.gameObject);

            if (_item_filtering == null) return;

            // Create representation of the new item
            var rep = item.create(_item_filtering.name,
                item_filtering_spot.position,
                item_filtering_spot.rotation,
                logistics_version: true);
            rep.transform.SetParent(item_filtering_spot);

            // Representation is just for show
            foreach (var c in rep.GetComponentsInChildren<Component>())
            {
                if (c is Transform) continue;
                if (c is Renderer) continue;
                if (c is MeshFilter) continue;
                Destroy(c);
            }
        }
    }
    item _item_filtering;

    item item_processing
    {
        get => _item_processing;
        set
        {
            _item_processing = value;
            if (_item_processing == null) return;
            _item_processing.transform.SetParent(item_processing_spot);
        }
    }
    item _item_processing;

    bool listener_set = false;
    Quaternion scales_reset_local_rot;
    float stage_progress = 0;
    bool direct_to_filtered = false;

    enum STAGE
    {
        WAITING,
        LOADING,
        UNLOADING,
        ROTATING,
        UNROTATING
    }
    STAGE stage;

    private void Start()
    {
        scales_reset_local_rot = scales.localRotation;
    }

    private void Update()
    {
        // Setup the listener for filter changes
        if (!listener_set && read_split_from.inventory != null)
        {
            inventory.on_change_func listener = () =>
            {
                // Read item that we're filtering from chest inventory
                item_filtering = null;
                foreach (var kv in read_split_from.inventory.contents())
                    if (kv.Key != null)
                    {
                        item_filtering = kv.Key;
                        break;
                    }
            };

            read_split_from.inventory.add_on_change_listener(listener);
            listener_set = true;
            listener(); // Invoke immediately
        }

        // Waiting for item
        if (stage == STAGE.WAITING)
        {
            // No item
            if (input.item_count == 0) return;

            // Release next item from input
            item_processing = input.release_item(0);

            // Load the item onto the weiging scales
            stage = STAGE.LOADING;
        }

        // Increment stage progress / potentially the stage itself
        stage_progress += Time.deltaTime / rotation_period;
        switch (stage)
        {
            case STAGE.LOADING:
                // Move item towards stage
                if (utils.move_towards(item_processing.transform,
                    item_processing_spot.position, Time.deltaTime * 2f))
                    stage_progress = 1.01f; // Arived, complete this stage
                break;

            case STAGE.UNLOADING:
                // Move item towards correct output
                Vector3 target = direct_to_filtered ?
                    filtered_output.input_point(item_processing.transform.position) :
                    other_output.input_point(item_processing.transform.position);
                if (utils.move_towards(item_processing.transform,
                    target, Time.deltaTime * 2f))
                    stage_progress = 1.01f; // Arrived, complete this stage
                break;

            case STAGE.ROTATING:
                // Rotate towards rotated position
                scales.localRotation = Quaternion.Lerp(
                    scales_reset_local_rot,
                    scales_rotated.localRotation,
                    stage_progress);
                break;

            case STAGE.UNROTATING:
                // Rotate towards reset position
                scales.localRotation = Quaternion.Lerp(
                    scales_rotated.localRotation,
                    scales_reset_local_rot,
                    stage_progress);
                break;
        }

        if (stage_progress >= 1f)
        {
            stage_progress = 0f;
            switch (stage)
            {
                case STAGE.LOADING:

                    if (item_processing.name == item_filtering?.name)
                    {
                        // Item is of the filtered type, move to unloading stage immediately
                        direct_to_filtered = true;
                        stage = STAGE.UNLOADING;
                        return;
                    }

                    // Item is not of the filtered type, we need
                    // to rotate it down to the unfiltered output
                    // before unloading
                    direct_to_filtered = false;
                    stage = STAGE.ROTATING;
                    break;

                case STAGE.UNLOADING:
                    // Unload item to the correct output
                    if (direct_to_filtered) filtered_output.add_item(item_processing);
                    else other_output.add_item(item_processing);
                    item_processing = null;
                    stage = direct_to_filtered ? STAGE.WAITING : STAGE.UNROTATING;
                    break;

                case STAGE.ROTATING:
                    // Rotation complete, unload item
                    stage = STAGE.UNLOADING;
                    break;

                case STAGE.UNROTATING:
                    // Un-rotation complete, return to waiting
                    stage = STAGE.WAITING;
                    break;

                case STAGE.WAITING:
                default: throw new System.Exception("Unkown/invalid stage!");
            }
        }
    }
}
