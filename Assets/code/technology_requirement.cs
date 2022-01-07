using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class technology_requirement : MonoBehaviour
{
    public technology technology;

    public bool satisfied => technology.complete;

    //##############//
    // STATIC STUFF //
    //##############//

    public static bool unlocked<T>(T t) where T : Component
    {
        foreach (var tr in t.GetComponents<technology_requirement>())
            if (!tr.satisfied)
                return false;
        return true;
    }
}
