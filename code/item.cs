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

    // base.name is of the form id_name (the id is used so saving an item
    // requires a fixed number of bytes, rather than a variable-size string)
    new public string name { get { return parse_id_name(base.name).Value; } }
    public int id { get { return parse_id_name(base.name).Key; } }

    // The name of the prefab I was created from
    public string prefab { get { return "items/" + base.name; } }

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
        // Start kinematic
        rigidbody = gameObject.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static KeyValuePair<int, string> parse_id_name(string to_parse)
    {
        int us_index = to_parse.IndexOf("_");
        int id = int.Parse(to_parse.Substring(0, us_index));
        string name = to_parse.Substring(us_index + 1);
        return new KeyValuePair<int, string>(id, name);
    }

    public static item spawn(string name, Vector3 position, Quaternion rotation)
    {
        var i = create(id_from_name(name), position, rotation);
        return i;
    }

    static item create(int id, Vector3 position, Quaternion rotation)
    {
        // Create a networked version of the  
        chunk parent = chunk.at(position);
        var i = load_from_id(id).inst();
        i.transform.SetParent(parent.transform);
        i.transform.position = position;
        i.transform.rotation = rotation;
        i.rigidbody = i.gameObject.AddComponent<Rigidbody>();
        return i;
    }

    // Lookup tables for items by name or id
    static Dictionary<int, item> item_by_id;
    static Dictionary<string, int> id_by_name;

    // Load the above lookup tables
    static void load_item_libraries()
    {
        item_by_id = new Dictionary<int, item>();
        id_by_name = new Dictionary<string, int>();

        foreach (var i in Resources.LoadAll<item>("items/"))
        {
            item_by_id[i.id] = i;
            id_by_name[i.name] = i.id;
        }
    }

    // Load an item by name
    public static item load_from_name(string name)
    {
        return load_from_id(id_from_name(name));
    }

    // Load an item by id
    public static item load_from_id(int id)
    {
        if (item_by_id == null)
            load_item_libraries();
        return item_by_id[id];
    }

    // Get an item id from an item name
    public static int id_from_name(string name)
    {
        if (id_by_name == null)
            load_item_libraries();
        return id_by_name[name];
    }
}