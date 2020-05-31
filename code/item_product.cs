using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_product : product
{
    public item item;
    public int min_count;
    public int max_count;

    public enum MODE
    {
        SIMPLE,
        RANDOM
    }
    public MODE mode;

    public override string crafting_string()
    {
        if (min_count == max_count)
        {
            int count = min_count;
            if (count > 1) return count + item.plural;
            else return item.display_name;
        }
        else return "between " + min_count + " and " +
                     max_count + " " + item.plural;

    }

    public override string product_name() { return item.display_name; }
    public override string product_name_plural() { return item.plural; }

    public override void create_in_inventory(inventory_section inv)
    {
        inv.add(item.name, Random.Range(min_count, max_count+1));
    }

    public override Sprite sprite()
    {
        return item.sprite;
    }
    
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(item_product))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            item_product ip = (item_product)target;

            var new_item = (item)UnityEditor.EditorGUILayout.ObjectField("item", ip.item, typeof(item), false);
            if (new_item != ip.item)
            {
                ip.item = new_item;
                UnityEditor.EditorUtility.SetDirty(ip);
            }

            var new_mode = (MODE)UnityEditor.EditorGUILayout.EnumPopup("mode", ip.mode);
            if (ip.mode != new_mode)
            {
                ip.mode = new_mode;
                UnityEditor.EditorUtility.SetDirty(ip);
            }

            switch (ip.mode)
            {
                case MODE.SIMPLE:
                    int new_count = UnityEditor.EditorGUILayout.IntField("count", ip.min_count);

                    if (ip.min_count != new_count || ip.max_count != new_count)
                    {
                        ip.min_count = new_count;
                        ip.max_count = new_count;
                        UnityEditor.EditorUtility.SetDirty(ip);
                    }

                    break;

                case MODE.RANDOM:
                    int new_min = UnityEditor.EditorGUILayout.IntField("min count", ip.min_count);
                    int new_max = UnityEditor.EditorGUILayout.IntField("max count", ip.max_count);

                    if (ip.min_count != new_min)
                    {
                        ip.min_count = new_min;
                        UnityEditor.EditorUtility.SetDirty(ip);
                    }

                    if (ip.max_count != new_max)
                    {
                        ip.max_count = new_max;
                        UnityEditor.EditorUtility.SetDirty(ip);
                    }

                    break;

                default:
                    throw new System.Exception("Unkown mode!");
            }
        }
    }
#endif

}
