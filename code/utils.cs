using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class utils
{
	// Create an exact copy of the object t
    public static T inst<T>(this T t) where T : Object
    {
        var ret = Object.Instantiate(t);
        ret.name = t.name;
        return ret;
    }
}
