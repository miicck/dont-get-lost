using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class component_search
{
    const string MENU_ROOT = "Tools/Component Search/";

    static List<GameObject> found = new List<GameObject>();
    static int cycle_index = 0;

    [UnityEditor.MenuItem(MENU_ROOT + "Find components by type")]
    public static void find_components_of_type()
    {
        var obj = UnityEditor.Selection.activeObject;
        if (obj is UnityEditor.MonoScript)
        {
            var ms = (UnityEditor.MonoScript)obj;
            var type = ms.GetClass();

            var allAssets = UnityEditor.AssetDatabase.GetAllAssetPaths().Where(
                path => path.StartsWith("Assets/")).ToArray();
            var objs = allAssets.Select(a => UnityEditor.AssetDatabase.LoadAssetAtPath(
                a, typeof(GameObject)) as GameObject).Where(a => a != null).ToArray();

            found = new List<GameObject>();
            cycle_index = 0;
            foreach (var o in objs)
            {
                var t = o.GetComponentInChildren(type);
                if (t != null) found.Add(o);
            }

            UnityEditor.Selection.objects = found.ToArray();
            Debug.Log("Found " + found.Count + " objects (selected), use " + MENU_ROOT + "/Cycle to cycle through them.");
        }
        else Debug.LogError("Please select a .cs script before using this tool.");
    }

    [UnityEditor.MenuItem(MENU_ROOT + "Cycle")]
    public static void cycle()
    {
        UnityEditor.Selection.objects = new Object[] { found[cycle_index] };
        cycle_index = (cycle_index + 1) % found.Count;
    }
}
