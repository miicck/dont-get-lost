using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A melee weapon that can be used 
/// for a specific purpose. </summary>
public class tool : melee_weapon
{
    /// <summary> Particular kinds of tools. </summary>
    public enum TYPE
    {
        AXE,
        PICKAXE,
    }

    /// <summary> Converts a <see cref="tool.TYPE"/> to a string. </summary>
    public static string type_to_name(TYPE t)
    {
        string name = System.Enum.GetName(typeof(TYPE), t);
        return name.ToLower();
    }

    /// <summary> Gets a sprite representing a particular type. </summary>
    public static Sprite type_to_sprite(TYPE t)
    {
        return Resources.Load<Sprite>("sprites/" + type_to_name(t));
    }

    /// <summary> How good a tool is at it's job. </summary>
    public enum QUALITY
    {
        TERRIBLE = 0,
        POOR = 1,
        MEDIOCRE = 2,
        AVERAGE = 3,
        DECENT = 4,
        GOOD = 5,
        FINE = 6,
        EXCELLENT = 7,
        EXQUISITE = 8,
        LEGENDARY = 9
    }

    /// <summary> Convert a <see cref="tool.QUALITY"/> to a string. </summary>
    public static string quality_to_name(QUALITY q)
    {
        string name = System.Enum.GetName(typeof(QUALITY), q);
        return name.ToLower();
    }

    /// <summary> The type of this tool. </summary>
    public TYPE type;

    /// <summary> The quality of this tool. </summary>
    public QUALITY quality;
}
