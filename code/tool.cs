using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class tool : MonoBehaviour
{
    // The library of tools, loaded from the resources/tools directory.
    static Dictionary<string, tool> tool_library;
    public static tool loop_up(string name)
    {
        if (tool_library == null)
        {
            tool_library = new Dictionary<string, tool>();
            foreach (var t in Resources.LoadAll<tool>("tools"))
                tool_library[t.name] = t;
        }
        return tool_library[name];
    }
}
