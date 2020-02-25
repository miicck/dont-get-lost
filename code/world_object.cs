using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class world_object : MonoBehaviour
{
	// The library of world objects, indexed by type
    static Dictionary<string, world_object[]> library =
        new Dictionary<string, world_object[]>();

	// Create a random world object from the given
	// world_objects type folder.
    public static world_object create_random(string type)
    {
        world_object[] options = null;
        if (!library.TryGetValue(type, out options))
        {
            options = Resources.LoadAll<world_object>("world_objects/" + type);
            library[type] = options;
        }
        return options[Random.Range(0, options.Length)].inst();
    }
}