using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> When created, a networked_object_spawner will wait until it is within
/// render range of the player and will create a networked object of the given type, 
/// unless one is produced by the server within a given time limit. </summary>
public class networked_object_spawner : natural_world_object
{ 
    public string prefab_to_spawn;

    spawned_by_world_object created;

    public override void on_placement()
    {
        base.on_placement();
        Invoke("check_in_range", 0.1f);
    }

    void check_in_range()
    {
        var dist = (transform.position - player.current.transform.position).magnitude;
        if (dist > game.render_range * 0.75f)
        {
            // Not in range, check again in a bit
            Invoke("check_in_range", 0.1f);
            return;
        }

        // In range, trigger creation unless the server provides
        // us with a networked version soon
        Invoke("create", 2f);
    }

    public void created_from_network(spawned_by_world_object created)
    {
        this.created = created;

        // We've got a networked object, no need to
        // check to see if we should create one
        CancelInvoke("check_in_range");
        CancelInvoke("create");
    }

    void create()
    {
        if (this.created != null) return; // Already loaded from network, don't create another
        created = (spawned_by_world_object)client.create(transform.position, prefab_to_spawn);
        created.set_spawner(this);
        created.on_first_spawn();
    }   
}

public class spawned_by_world_object : networked
{
    networked_variables.net_int chunk_x;
    networked_variables.net_int chunk_z;
    networked_variables.net_int x_in_chunk;
    networked_variables.net_int z_in_chunk;
    protected networked_object_spawner spawned_by { get; private set; }

    void on_get_coords()
    {
        CancelInvoke("set_spawner");
        Invoke("set_spawner", 0);
    }

    public override void on_init_network_variables()
    {
        chunk_x = new networked_variables.net_int();
        chunk_z = new networked_variables.net_int();
        x_in_chunk = new networked_variables.net_int();
        z_in_chunk = new networked_variables.net_int();

        chunk_x.on_change = on_get_coords;
        chunk_z.on_change = on_get_coords;
        x_in_chunk.on_change = on_get_coords;
        z_in_chunk.on_change = on_get_coords;
    }

    void set_spawner()
    {
        chunk spawner_chunk = chunk.at(chunk_x.value, chunk_z.value, true);
        if (spawner_chunk == null)
        {
            // Wait for the spawn chunk to be fully generated
            Invoke("set_spawner", 0.5f);
            return;
        }

        var wo = spawner_chunk.get_object(x_in_chunk.value, z_in_chunk.value);

        if (wo != null && wo is networked_object_spawner)
        {
            // Let the networked_object_spawner know that 
            // we've been created from the network
            spawned_by = (networked_object_spawner)wo;
            spawned_by.created_from_network(this);
        }
        else
            throw new System.Exception("World object found is not a networked_world_object!");
    }

    public void set_spawner(networked_object_spawner spawner)
    {
        chunk_x.value = spawner.chunk.x;
        chunk_z.value = spawner.chunk.z;
        x_in_chunk.value = spawner.x_in_chunk;
        z_in_chunk.value = spawner.z_in_chunk;
        spawned_by = spawner;
    }

    public virtual void on_first_spawn() { }
}