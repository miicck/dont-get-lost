using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Part of a world object that can be harvested by hand
/// to yield a product, and takes time to regrow. The part will
/// be disabled until it regrows. </summary>
[RequireComponent(typeof(product))]
public class harvest_by_hand : MonoBehaviour, IInspectable
{
    public AudioClip play_on_pick;
    public float regrow_time = 1f;
    public float play_on_pick_volume = 1f;

    product[] products { get => GetComponents<product>(); }

    AudioSource source;

    /// <summary> Raycast for a harvest_by_hand object. </summary>
    public static harvest_by_hand raycast(Ray ray,
        out RaycastHit hit, float max_distance)
    {
        harvest_by_hand found = null;
        hit = default;
        float min_dis = Mathf.Infinity;

        foreach (var h in Physics.RaycastAll(ray, max_distance))
        {
            var hbh = h.transform.GetComponentInParent<harvest_by_hand>();
            float dis = (h.point - ray.origin).magnitude;
            if (dis < min_dis)
            {
                min_dis = dis;
                found = hbh;
                hit = h;
            }
        }

        return found;
    }

    public void on_pick()
    {
        if (source == null)
        {
            // Create the audio source if it does not already exist
            source = new GameObject("pick_audio").AddComponent<AudioSource>();
            source.transform.position = transform.position;
            source.transform.SetParent(transform.parent);
            source.clip = play_on_pick;
            source.volume = play_on_pick_volume;
            source.spatialBlend = 1f; // 3D
            source.playOnAwake = false;
        }

        // Play the harvest sound
        source.Play();

        // Get the world_object that this belongs to
        var wo = GetComponentInParent<world_object>();
        if (wo == null)
        {
            var msg = "Objects harvestable by hand must be a child of a world_object!";
            throw new System.Exception(msg);
        }

        // When an object is harvested, the world object that it belongs to
        // has a networked object (world_object_harvested) added to it,
        // so that the object disappears on all clients. It works in this
        // subtractive way to reduce the total number of networked objects.
        var woh = (world_object_harvested)client.create(
            wo.transform.position, "misc/world_object_harvested");

        woh.x_in_chunk.value = wo.x_in_chunk;
        woh.z_in_chunk.value = wo.z_in_chunk;
        woh.chunk_x.value = wo.chunk.x;
        woh.chunk_z.value = wo.chunk.z;
        woh.timeout.value = regrow_time;

        // Create the product in the player inventory
        foreach (var p in products)
            p.create_in(player.current.inventory);
    }

    //##############//
    // IInspectable //
    //##############//

    public string inspect_info()
    {
        return product.product_quantities_list(products) + " can bn harvested by hand";
    }

    public Sprite main_sprite()
    {
        return products[0].sprite();
    }

    public Sprite secondary_sprite()
    {
        return Resources.Load<Sprite>("sprites/default_interact_cursor");
    }

    //###############//
    // CUSTOM EDITOR //
    //###############//

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(harvest_by_hand))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var pbh = (harvest_by_hand)target;

            // Harvest sound defaults to click_1
            if (pbh.play_on_pick == null)
            {
                pbh.play_on_pick = Resources.Load<AudioClip>("sounds/click_1");
                UnityEditor.EditorUtility.SetDirty(pbh);
            }
        }
    }
#endif
}