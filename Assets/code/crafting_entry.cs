using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class crafting_entry : MonoBehaviour
{
    public UnityEngine.UI.Text text;
    public UnityEngine.UI.Button button;
    public UnityEngine.UI.Image image;

    public static crafting_entry create()
    {
        return Resources.Load<crafting_entry>("ui/crafting_entry").inst();
    }
}
