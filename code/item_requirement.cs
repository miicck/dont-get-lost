using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A test that an item can satisfy. </summary>
public class item_requirement : MonoBehaviour
{
    /// <summary> The type of test to carry out. </summary>
    public enum MODE
    {
        SPECIFIC_ITEM,
        TOOL_QUALITY,
    }
    public MODE mode = MODE.TOOL_QUALITY;

    public item item;
    public tool.TYPE tool_type = tool.TYPE.AXE;
    public tool.QUALITY tool_quality = tool.QUALITY.TERRIBLE;

    /// <summary> Returns true if the test passes. </summary>
    public bool satisfied(item item)
    {
        if (item == null) return false;
        switch (mode)
        {
            case MODE.SPECIFIC_ITEM:
                return item.name == this.item.name;

            case MODE.TOOL_QUALITY:
                if (item is tool)
                {
                    var t = (tool)item;
                    if (t.type != tool_type) return false;
                    if (t.quality < tool_quality) return false;
                    return true;
                }
                else return false;

            default:
                throw new System.Exception("Unkown item requirement mode!");
        }
    }

    /// <summary> Text to display when talking about the test. 
    /// Fits in a sentance of the form "doing this requires [display_name]".
    /// </summary>
    public string display_name
    {
        get
        {
            switch (mode)
            {
                case MODE.SPECIFIC_ITEM:
                    return item.display_name;

                case MODE.TOOL_QUALITY:
                    string tt = tool.type_to_name(tool_type);
                    string tq = tool.quality_to_name(tool_quality);
                    return utils.a_or_an(tt) + " " + tt + " of " + tq + " quality, or greater";

                default:
                    throw new System.Exception("Unkown item requirement mode!");
            }
        }
    }

    /// <summary> A picture representing this test. </summary>
    public Sprite sprite
    {
        get
        {
            switch (mode)
            {
                case MODE.SPECIFIC_ITEM:
                    return item.sprite;

                case MODE.TOOL_QUALITY:
                    return tool.type_to_sprite(tool_type);

                default:
                    throw new System.Exception("Unkown item requirement mode!");
            }
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(item_requirement))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var ir = (item_requirement)target;

            var new_mode = (MODE)UnityEditor.EditorGUILayout.EnumPopup("mode", ir.mode);
            if (new_mode != ir.mode)
            {
                ir.mode = new_mode;
                UnityEditor.EditorUtility.SetDirty(ir);
            }

            switch (ir.mode)
            {
                case MODE.SPECIFIC_ITEM:

                    var new_item = (item)UnityEditor.EditorGUILayout.ObjectField("item", ir.item, typeof(item), false);
                    if (new_item != ir.item)
                    {
                        ir.item = new_item;
                        UnityEditor.EditorUtility.SetDirty(ir);
                    }
                    break;

                case MODE.TOOL_QUALITY:

                    var new_type = (tool.TYPE)UnityEditor.EditorGUILayout.EnumPopup("tool type", ir.tool_type);
                    if (new_type != ir.tool_type)
                    {
                        ir.tool_type = new_type;
                        UnityEditor.EditorUtility.SetDirty(ir);
                    }

                    var new_quality = (tool.QUALITY)UnityEditor.EditorGUILayout.EnumPopup("tool quality", ir.tool_quality);
                    if (new_quality != ir.tool_quality)
                    {
                        ir.tool_quality = new_quality;
                        UnityEditor.EditorUtility.SetDirty(ir);
                    }
                    break;
            }
        }
    }
#endif
}
