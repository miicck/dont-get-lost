﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class character_spawner : spawned_by_world_object
{
    public string character_to_spawn = "characters/chicken";
    public int max_to_spawn = 1;
    public float max_range = 5f;

    public override float network_radius()
    {
        // Ensure the spawner is always 
        // loaded before the characters 
        // come in range
        return max_range;
    }

    character[] spawned { get => GetComponentsInChildren<character>(true); }

    private void Start()
    {
        InvokeRepeating("check_spawn", 0.5f + Random.Range(0, 0.5f), 0.5f);
    }

    void check_spawn()
    {
        if (network_id < 0)
            return; // Wait until registered

        int to_spawn = max_to_spawn - spawned.Length;
        if (to_spawn <= 0)
            return; // None left to spawn

        for (int i = 0; i < to_spawn; ++i)
            client.create(transform.position, character_to_spawn, parent: this);
    }

    public override void on_add_networked_child(networked child)
    {
        // Children start active only if the chunk has already been
        // generated, otherwise they wait to be activated when the chunk is
        // generated.
        child.gameObject.SetActive(chunk_generated);
        if (!chunk_generated)
        {
            CancelInvoke("check_chunk");
            Invoke("check_chunk", 1f);
        }
    }

    bool chunk_generated = false;
    void check_chunk()
    {
        var c = chunk.at(transform.position, true);
        if (c == null)
        {
            Invoke("check_chunk", 1f);
            return;
        }

        chunk_generated = true;
        foreach (var s in spawned)
        {
            s.gameObject.SetActive(true);
            s.controller = new spawner_character_controller(this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, max_range);
    }
}

/// <summary> Override the default character behaviour so the 
/// character stays within a given range of the spawner. </summary>
public class spawner_character_controller : default_character_control
{
    character_spawner spawner;

    public spawner_character_controller(character_spawner spawner)
    {
        this.spawner = spawner;
    }

    protected override bool is_allowed_at(Vector3 v)
    {
        return base.is_allowed_at(v) && (v - spawner.transform.position).magnitude < spawner.max_range;
    }
}