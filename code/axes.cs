
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class axes : MonoBehaviour
{
    // #pragma is to stop it complaining about these 
    // not being initialized, because they are initialized
    // through reflection in the Editor.
#pragma warning disable 0649
    [SerializeField] GameObject x_axis;
    [SerializeField] GameObject y_axis;
    [SerializeField] GameObject z_axis;
#pragma warning restore 0649

    private void Start()
    {
        // Always render on top (in particular, stops the sky 
        // from rendering in front of the axes)
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.material.renderQueue = 5000;
    }

    public enum AXIS
    {
        NONE,X,Y,Z
    }

    public GameObject get_axis(AXIS a)
    {
        switch(a)
        {
            case AXIS.X: return x_axis;
            case AXIS.Y: return y_axis;
            case AXIS.Z: return z_axis;
            case AXIS.NONE: return null;
            default: throw new System.Exception("Unkown axis!");
        }
    }

    void highlight(GameObject a, bool highlight)
    {
        foreach (var r in a.GetComponentsInChildren<Renderer>())
            r.material.color = new Color(
                r.material.color.r,
                r.material.color.g,
                r.material.color.b,
                highlight ? 1f : 0.05f
            );
    }

    public void highlight_axis(AXIS a)
    {
        highlight(x_axis, false);
        highlight(y_axis, false);
        highlight(z_axis, false);
        if (a != AXIS.NONE)
            highlight(get_axis(a), true);
    }
}
