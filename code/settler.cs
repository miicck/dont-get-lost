using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A memeber of a settlment. </summary>
public class settler : networked
{
    public bed bed { get => GetComponentInParent<bed>(); }
    item_requirement[] requirements { get => GetComponents<item_requirement>(); }

    /// <summary> Check if a set of fixtures is enough 
    /// for this settler to move in. </summary>
    public bool requirements_satisfied(IEnumerable<fixture> fixtures)
    {
        foreach (var r in requirements)
        {
            bool satisfied = false;
            foreach (var f in fixtures)
                if (r.satisfied(f))
                {
                    satisfied = true;
                    break;
                }

            if (!satisfied)
                return false;
        }

        return true;
    }
}
