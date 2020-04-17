using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item : MonoBehaviour
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

    //############//
    // PLAYER USE //
    //############//

    // The possible results of using this item
    public enum USE_RESULT
    {
        UNDERWAY,
        COMPLETE,
    }

    // Use the equipped version of this item
    public virtual USE_RESULT on_use_start(player.USE_TYPE use_type) { return USE_RESULT.COMPLETE; }
    public virtual USE_RESULT on_use_continue(player.USE_TYPE use_type) { return USE_RESULT.COMPLETE; }
    public virtual void on_use_end(player.USE_TYPE use_type) { }

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
        {
            popup_message.create("+ 1 " + name);
            Destroy(this.gameObject);
        }
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

    //###############//
    // SERIALIZATION //
    //###############//

    public byte[] serialize()
    {
        const int IS = sizeof(int);
        const int FS = sizeof(float);

        // Floating point values to serialize
        Vector3 euler = transform.rotation.eulerAngles;
        float[] floats = new float[]
        {
            transform.position.x,
            transform.position.y,
            transform.position.z,
            transform.rotation.x,
            transform.rotation.y,
            transform.rotation.z,
            transform.rotation.w
        };

        // Integers to serialize
        int[] ints = new int[]
        {
            id,
            rigidbody.isKinematic ? 1 : 0,
        };

        // Serialize into byte array
        int length = floats.Length * FS + ints.Length * IS;
        var bytes = new byte[length];

        for (int i = 0; i < ints.Length; ++i)
        {
            var ib = System.BitConverter.GetBytes(ints[i]);
            System.Buffer.BlockCopy(ib, 0, bytes, i * IS, IS);
        }

        for (int i = 0; i < floats.Length; ++i)
        {
            var fb = System.BitConverter.GetBytes(floats[i]);
            System.Buffer.BlockCopy(fb, 0, bytes, ints.Length * IS + i * FS, FS);
        }

        return bytes;
    }

    public static item deserailize(byte[] bytes)
    {
        const int IS = sizeof(int);
        const int FS = sizeof(float);

        // Deserailize id
        int id = System.BitConverter.ToInt32(bytes, 0);
        bool is_kinematic = System.BitConverter.ToInt32(bytes, IS) != 0;

        // Deserailzie position
        Vector3 position;
        position.x = System.BitConverter.ToSingle(bytes, IS * 2);
        position.y = System.BitConverter.ToSingle(bytes, IS * 2 + FS);
        position.z = System.BitConverter.ToSingle(bytes, IS * 2 + FS * 2);

        // Desearialize rotation
        Quaternion rot = new Quaternion(
            System.BitConverter.ToSingle(bytes, IS * 2 + FS * 3),
            System.BitConverter.ToSingle(bytes, IS * 2 + FS * 4),
            System.BitConverter.ToSingle(bytes, IS * 2 + FS * 5),
            System.BitConverter.ToSingle(bytes, IS * 2 + FS * 6)
            );

        // Create the item
        var i = create(id, position, rot);
        i.rigidbody.isKinematic = is_kinematic;
        return i;
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
        var i = load_from_id(id).inst();
        i.transform.position = position;
        i.transform.rotation = rotation;
        i.rigidbody = i.gameObject.AddComponent<Rigidbody>();
        i.transform.SetParent(chunk.at(i.transform.position).transform);
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