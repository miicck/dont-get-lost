using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class filter_splitter : MonoBehaviour
{
    public chest read_split_from;
    public item_input input;
    public item_output split_output;
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
            _item_processing.transform.localPosition = Vector3.zero;
            _item_processing.transform.localRotation = Quaternion.identity;
        }
    }
    item _item_processing;

    bool listener_set = false;
    Quaternion scales_reset_local_rot;
    float stage_progress = 0;

    enum STAGE
    {
        WAITING,
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

            if (item_processing.name == item_filtering?.name)
            {
                // Item is of the filtered type, output immediately
                // and stay in the waiting stage
                split_output.add_item(item_processing);
                item_processing = null;
                return;
            }

            // Item is not of the filtered type - we need
            // to rotate the scales.
            stage = STAGE.ROTATING;
        }

        // Increment stage progress / potentially the stage itself
        stage_progress += Time.deltaTime / rotation_period;
        switch (stage)
        {
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

        if (stage_progress > 1f)
        {
            stage_progress = 0f;
            switch (stage)
            {
                case STAGE.ROTATING:
                    // Rotation complete, drop off item
                    other_output.add_item(item_processing);
                    item_processing = null;
                    stage = STAGE.UNROTATING;
                    break;

                case STAGE.UNROTATING:
                    // Cycle complete, return to waiting
                    stage = STAGE.WAITING;
                    break;

                case STAGE.WAITING:
                default: throw new System.Exception("Unkown/invalid stage!");
            }
        }
    }
}
