using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A product is triggered by some event, leading to 
/// items potentially being added to the player inventory. </summary>
public class product : MonoBehaviour
{
    /// <summary> The different ways that a product can be created. </summary>
    public enum MODE
    {
        SIMPLE,
        RANDOM_AMOUNT,
        PROBABILITY
    }

    /// <summary> The way this product is created. </summary>
    public MODE mode;

    /// <summary> The item (if any) that this product produces. </summary>
    public item item;

    /// <summary> The minimum count of this product to be produced. </summary>
    public int min_count = 1;

    /// <summary> The maximum count of this product to be produced. </summary>
    public int max_count = 1;

    /// <summary> The expected number of attempts to obtain this product. </summary>
    public int one_in_chance = 1;

    /// <summary> The item created by an automatic harvester. </summary>
    public virtual item auto_item => item;

    /// <summary> The display name of this product. </summary>
    public virtual string product_name()
    {
        switch (mode)
        {
            case MODE.SIMPLE:
            case MODE.RANDOM_AMOUNT:
            case MODE.PROBABILITY:
                return item.display_name;

            default:
                throw new System.Exception("Unkown product mode!");
        }
    }

    /// <summary> The display name of this product, pluralized. </summary>
    public virtual string product_name_plural()
    {
        switch (mode)
        {
            case MODE.SIMPLE:
            case MODE.RANDOM_AMOUNT:
            case MODE.PROBABILITY:
                return item.plural;

            default:
                throw new System.Exception("Unkown product mode!");
        }
    }

    /// <summary> The quantity and (appropriately pluralized) name. </summary>
    public virtual string product_name_quantity()
    {
        switch (mode)
        {
            case MODE.SIMPLE:
                if (min_count < 2) return item.display_name;
                return min_count + " " + item.plural;

            case MODE.RANDOM_AMOUNT:
                return "between " + min_count + " and " +
                    max_count + " " + item.plural;

            case MODE.PROBABILITY:
                string ret = null;
                if (min_count != max_count)
                    ret = "between " + min_count + " and " + max_count;
                else
                    ret = "" + min_count;
                if (max_count > 1)
                    ret += " " + item.plural;
                else
                    ret += " " + item.display_name;

                ret += " with a one in " + one_in_chance + " chance";
                return ret;

            default:
                throw new System.Exception("Unkown product mode!");
        }
    }

    /// <summary> Called when this product is produced in the given inventory. </summary>
    public virtual void create_in(IItemCollection inv)
    {
        switch (mode)
        {
            case MODE.SIMPLE:
            case MODE.RANDOM_AMOUNT:
                inv.add(item, Random.Range(min_count, max_count + 1));
                break;

            case MODE.PROBABILITY:
                float prob = 1f / one_in_chance;
                if (Random.Range(0, 1f) < prob)
                    inv.add(item, Random.Range(min_count, max_count + 1));
                break;

            default:
                throw new System.Exception("Unkown product mode!");
        }
    }

    /// <summary> A sprite representing the product. </summary>
    public virtual Sprite sprite()
    {
        switch (mode)
        {
            case MODE.SIMPLE:
            case MODE.RANDOM_AMOUNT:
            case MODE.PROBABILITY:
                return item.sprite;

            default:
                throw new System.Exception("Unkown product mode!");
        }
    }

    public void copy_to(product other)
    {
        other.mode = mode;
        other.item = item;
        other.min_count = min_count;
        other.max_count = max_count;
        other.one_in_chance = one_in_chance;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    /// <summary> Convert a list of products to a string describing that list. </summary>
    public static string product_quantities_list(IList<product> products)
    {
        string ret = "";
        for (int i = 0; i < products.Count - 1; ++i)
            ret += products[i].product_name_quantity() + ", ";

        if (products.Count > 1)
        {
            ret = ret.Substring(0, ret.Length - 2);
            ret += " and " + products[products.Count - 1].product_name_quantity();
        }
        else ret = products[0].product_name_quantity();

        return ret;
    }

    public static string product_plurals_list(IList<product> products)
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

    //###############//
    // CUSTOM EDITOR //
    //###############//

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(product))]
    class editor : UnityEditor.Editor
    {
        /// <summary> Create a dropdown to select the product mode. </summary>
        void select_mode(product p)
        {
            var new_mode = (MODE)UnityEditor.EditorGUILayout.EnumPopup("mode", p.mode);
            if (p.mode != new_mode)
            {
                p.mode = new_mode;
                UnityEditor.EditorUtility.SetDirty(p);
            }
        }

        /// <summary> Create a dropdown to select the item produced. </summary>
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
                case MODE.PROBABILITY:
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

                    if (p.mode == MODE.PROBABILITY)
                    {
                        int new_one_in_chance = UnityEditor.EditorGUILayout.IntField("one in x prob", p.one_in_chance);
                        if (p.one_in_chance != new_one_in_chance)
                        {
                            p.one_in_chance = new_one_in_chance;
                            UnityEditor.EditorUtility.SetDirty(p);
                        }
                    }

                    break;

                default:
                    throw new System.Exception("Unkown mode!");
            }
        }
    }
#endif
}