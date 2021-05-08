using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class skin : MonoBehaviour
{
    public Color color
    {
        get => utils.get_color(GetComponent<Renderer>()?.material);
        set => utils.set_color(GetComponent<Renderer>()?.material, value);
    }
}
