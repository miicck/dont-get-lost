using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class version_number_display : MonoBehaviour
{
    void Start()
    {
        GetComponent<UnityEngine.UI.Text>().text = "Version " + version_control.info.version;
    }
}
