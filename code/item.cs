using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item : networked
{
    //###########//
    // VARIABLES //
    //###########//

    public Sprite sprite; // The sprite represeting this item in inventories etc
    new public Rigidbody rigidbody { get; private set; } // The rigidbody attached to this item in-world
    Transform carry_pivot; // The point we are carrying this item by in carry mode

    //############//
    // PLAYER USE //
    //############//

    public struct use_result
    {
        public bool underway;
        public bool allows_look;
        public bool allows_move;
        public bool allows_throw;

        public static use_result complete
        {
            get => new use_result()
            {
                underway = false,
                allows_look = true,
                allows_move = true,
                allows_throw = true
            };
        }

        public static use_result underway_allows_none
        {
            get => new use_result()
            {
                underway = true,
                allows_look = false,
                allows_move = false,
                allows_throw = false
            };
        }

        public static use_result underway_allows_all
        {
            get => new use_result()
            {
                underway = true,
                allows_look = true,
                allows_move = true,
                allows_throw = true
            };
        }
    }

    // Use the equipped version of this item
    public virtual use_result on_use_start(player.USE_TYPE use_type) { return use_result.complete; }
    public virtual use_result on_use_continue(player.USE_TYPE use_type) { return use_result.complete; }
    public virtual void on_use_end(player.USE_TYPE use_type) { }
    public virtual bool allow_left_click_held_down() { return false; }
    public virtual bool allow_right_click_held_down() { return false; }

    public void carry(RaycastHit point_hit)
    {
        // Create the pivot point that we clicked the item at
        carry_pivot = new GameObject("pivot").transform;
        carry_pivot.SetParent(transform);
        carry_pivot.transform.position = point_hit.point;
        carry_pivot.rotation = player.current.camera.transform.rotation;

        // Item is under player ownership
        transform.SetParent(player.current.camera.transform);

        // Setup item in carry mode
        rigidbody.isKinematic = true;
        rigidbody.detectCollisions = false;
    }

    public void stop_carry()
    {
        // Put the item back in physics mode
        transform.SetParent(chunk.at(transform.position).transform);
        rigidbody.isKinematic = false;
        rigidbody.detectCollisions = true;
    }

    public void pick_up()
    {
        // Attempt to pick up the item (put it in the player
        // inventory/destroy in-game representation)
        if (player.current.inventory.add(name, 1))
            delete();
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    private void Update()
    {
        // Fallen through the map, return to sensible place
        if (transform.position.y < -10f)
        {
            rigidbody.velocity = Vector3.zero;
            transform.position = player.current.camera.transform.position;
        }
    }

    //############//
    // NETWORKING //
    //############//

    public networked_variable.net_quaternion networked_rotation;

    public override void on_init_network_variables()
    {
        networked_rotation = new networked_variable.net_quaternion();
        transform.rotation = Quaternion.identity;
        networked_rotation.on_change = (rot) => transform.rotation = rot;
    }

    public override void on_create()
    {
        networked_rotation.value = transform.rotation;
    }

    //################//
    // STATIC METHODS //
    //################//

    /// <summary> Create an item. Attaches a rigidbody to 
    /// the item in the process. </summary>
    public static item create(string name,
        Vector3 position, Quaternion rotation,
        bool kinematic = true, bool networked = false,
        networked network_parent = null)
    {
        item item = null;

        if (networked)
        {
            item = (item)client.create(position, "items/" + name,
                rotation: rotation, parent: network_parent);
        }
        else
        {
            item = Resources.Load<item>("items/" + name);
            if (item == null)
                throw new System.Exception("Could not find the item: " + name);
            item = item.inst();
        }

        item.rigidbody = item.gameObject.AddComponent<Rigidbody>();
        item.rigidbody.isKinematic = kinematic;

        return item;
    }
}