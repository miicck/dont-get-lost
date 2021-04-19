using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary> Class used to extract the neccassary information from
/// the unity project for the standalone server to function. </summary>
public static class gen_server_data
{
    [UnityEditor.MenuItem("Tools/Gen server data/generate")]
    static void gen()
    {
        version_control.generate_server_data();
    }
}
