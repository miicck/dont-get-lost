using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pick_by_hand : MonoBehaviour
{
    public float regrow_time = 1f;
    public string item_to_pick = "lettuce";
    public int quantity_to_pick = 1;
    public AudioClip play_on_pick;
    public float play_on_pick_volume = 1f;

    AudioSource source;

    public void on_pick()
    {
        if (source == null)
        {
            source = new GameObject("pick_audio").AddComponent<AudioSource>();
            source.transform.position = transform.position;
            source.transform.SetParent(transform.parent);

            if (play_on_pick != null)
            {
                source.clip = play_on_pick;
                source.volume = play_on_pick_volume;
            }
            else
            {
                source.clip = Resources.Load<AudioClip>("sounds/click_1");
                source.volume = 0.5f;
            }

            source.spatialBlend = 1f; // 3D
            source.playOnAwake = false;
        }

        source.Play();

        var wo = GetComponentInParent<world_object>();

        var woh = (world_object_harvested)
            client.create(wo.transform.position, 
            "misc/world_object_harvested");

        woh.x_in_chunk.value = wo.x_in_chunk;
        woh.z_in_chunk.value = wo.z_in_chunk;
        woh.timeout.value = regrow_time;

        player.current.inventory.add(item_to_pick, quantity_to_pick);
    }
}