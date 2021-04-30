
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A set of XYZ axes. </summary>
public class axes : MonoBehaviour, IDontBlockItemLogisitcs
{
    // #pragma is to stop it complaining about these 
    // not being initialized, because they are initialized
    // through reflection in the Editor.
#pragma warning disable 0649
    [SerializeField] GameObject x_axis;
    [SerializeField] GameObject y_axis;
    [SerializeField] GameObject z_axis;
#pragma warning restore 0649

    public enum AXIS
    {
        NONE, X, Y, Z
    }

    public GameObject get_axis(AXIS a)
    {
        switch (a)
        {
            case AXIS.X: return x_axis;
            case AXIS.Y: return y_axis;
            case AXIS.Z: return z_axis;
            case AXIS.NONE: return null;
            default: throw new System.Exception("Unkown axis!");
        }
    }

    public AXIS test_is_part_of_axis(Transform t)
    {
        if (t.IsChildOf(x_axis.transform)) return AXIS.X;
        if (t.IsChildOf(y_axis.transform)) return AXIS.Y;
        if (t.IsChildOf(z_axis.transform)) return AXIS.Z;
        return AXIS.NONE;
    }

    Dictionary<Renderer, Color> initial_colors
    {
        get
        {
            if (_initial_colors == null)
            {
                _initial_colors = new Dictionary<Renderer, Color>();
                foreach (var a in new GameObject[] { x_axis, y_axis, z_axis })
                    foreach (var r in GetComponentsInChildren<Renderer>())
                        _initial_colors[r] = r.material.color;
            }
            return _initial_colors;
        }
    }
    Dictionary<Renderer, Color> _initial_colors;

    void highlight(GameObject a, bool highlight)
    {
        float b = highlight ? 0.5f : 1f;
        foreach (var r in a.GetComponentsInChildren<Renderer>())
        {
            var init_color = initial_colors[r];
            r.material.color = new Color(
                init_color.r * b + (1 - b),
                init_color.g * b + (1 - b),
                init_color.b * b + (1 - b)
            );
        }
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
