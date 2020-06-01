using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class product : MonoBehaviour
{
    public enum MODE
    {
        SIMPLE,
        RANDOM_AMOUNT,
    }

    public MODE mode;
    public item item;
    public int min_count = 1;
    public int max_count = 1;

    public string product_name()
    {
        return item.display_name;
    }


    public string product_name_plural()
    {
        return item.plural;
    }

    public void create_in_inventory(inventory_section inv)
    {
        inv.add(item.name, Random.Range(min_count, max_count + 1));
    }

    public Sprite sprite()
    {
        return item.sprite;
    }

    public static string product_list(IList<product> products)
    {
        string ret = "";
        for (int i = 0; i < products.Count - 1; ++i)
            ret += products[i].product_name_plural() + ", ";

        if (products.Count > 1)
        {
            ret = ret.Substring(0, ret.Length - 2);
            ret += " and " + products[products.Count - 1].product_name_plural();
        }
        else ret = products[0].product_name_plural();

        return ret;
    }


#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(product))]
    class editor : UnityEditor.Editor
    {
        void select_mode(product p)
        {
            var new_mode = (MODE)UnityEditor.EditorGUILayout.EnumPopup("mode", p.mode);
            if (p.mode != new_mode)
            {
                p.mode = new_mode;
                UnityEditor.EditorUtility.SetDirty(p);
            }
        }

        void select_item(product p)
        {
            var new_item = (item)UnityEditor.EditorGUILayout.ObjectField("item", p.item, typeof(item), false);
            if (new_item != p.item)
            {
                p.item = new_item;
                UnityEditor.EditorUtility.SetDirty(p);
            }
        }

        public override void OnInspectorGUI()
        {
            product p = (product)target;

            switch (p.mode)
            {
                case MODE.SIMPLE:
                    select_mode(p);
                    select_item(p);

                    int new_count = UnityEditor.EditorGUILayout.IntField("count", p.min_count);

                    if (p.min_count != new_count || p.max_count != new_count)
                    {
                        p.min_count = new_count;
                        p.max_count = new_count;
                        UnityEditor.EditorUtility.SetDirty(p);
                    }

                    break;

                case MODE.RANDOM_AMOUNT:
                    select_mode(p);
                    select_item(p);

                    int new_min = UnityEditor.EditorGUILayout.IntField("min count", p.min_count);
                    int new_max = UnityEditor.EditorGUILayout.IntField("max count", p.max_count);

                    if (p.min_count != new_min)
                    {
                        p.min_count = new_min;
                        UnityEditor.EditorUtility.SetDirty(p);
                    }

                    if (p.max_count != new_max)
                    {
                        p.max_count = new_max;
                        UnityEditor.EditorUtility.SetDirty(p);
                    }

                    break;

                default:
                    throw new System.Exception("Unkown mode!");
            }
        }
    }
#endif
}
