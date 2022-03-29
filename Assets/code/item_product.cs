using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A product is triggered by some event, leading to 
/// items potentially being added to the player inventory. </summary>
public class item_product : product
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

    /// <summary> The item created by an automatic crafter. </summary>
    public virtual item auto_item => item;

    /// <summary> The display name of this product. </summary>
    public override string product_name()
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
    public override string product_name_plural()
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
    public override string product_name_quantity()
    {
        if (item == null)
            throw new System.Exception("No item assigned to product " + name);

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
                    
                ret += " with a 1 in " + one_in_chance + " chance";
                return ret;

            default:
                throw new System.Exception("Unkown product mode!");
        }
    }

    /// <summary> Is this product unlocked for the player? </summary>
    public override bool unlocked
    {
        get
        {
            var item_unlocked = item == null || technology_requirement.unlocked(item);
            return item_unlocked && technology_requirement.unlocked(this);
        }
    }

    /// <summary> Called when this product is produced in the given inventory. </summary>
    public override void create_in(IItemCollection inv, int count = 1, bool track_production = false)
    {
        int to_add = 0;
        switch (mode)
        {
            case MODE.SIMPLE:
            case MODE.RANDOM_AMOUNT:
                to_add = Random.Range(min_count * count, max_count * count + 1);
                break;

            case MODE.PROBABILITY:
                if (Random.Range(0, 1f) > 1f / one_in_chance) return;
                to_add = Random.Range(min_count * count, max_count * count + 1);
                break;

            default:
                Debug.LogError("Unkown product mode!");
                return;
        }

        inv.add(item, to_add);
        if (track_production) production_tracker.register_product(item, to_add);
    }

    public override float average_amount_produced(item i)
    {
        // This product doesn't make that item
        if (i == null || item == null || i.name != item.name) return 0;

        switch (mode)
        {
            case MODE.SIMPLE:
            case MODE.RANDOM_AMOUNT:
                return (min_count + max_count) / 2f;

            case MODE.PROBABILITY:
                return (min_count + max_count) / (2f * one_in_chance);

            default:
                Debug.LogError("Unkown product mode!");
                return 0;
        }
    }

    public override void create_in_node(item_node node, bool track_production = false)
    {
        int count = 0;

        switch (mode)
        {
            case MODE.SIMPLE:
            case MODE.RANDOM_AMOUNT:
                count = Random.Range(min_count, max_count + 1);
                break;

            case MODE.PROBABILITY:
                float prob = 1f / one_in_chance;
                if (Random.Range(0, 1f) < prob)
                    count = Random.Range(min_count, max_count + 1);
                break;

            default:
                Debug.LogError("Unkown product mode!");
                return;
        }

        if (count == 0) return;
        if (track_production) production_tracker.register_product(item, count);
        for (int i = 0; i < count; ++i)
        {
            var itm = item.create(item.name, node.transform.position,
                Quaternion.identity, logistics_version: true);
            node.add_item(itm);
        }
    }

    /// <summary> A sprite representing the product. </summary>
    public override Sprite sprite
    {
        get
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
    }

    public void copy_to(item_product other)
    {
        other.mode = mode;
        other.item = item;
        other.min_count = min_count;
        other.max_count = max_count;
        other.one_in_chance = one_in_chance;
    }

    //###############//
    // CUSTOM EDITOR //
    //###############//

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(item_product))]
    class editor : UnityEditor.Editor
    {
        /// <summary> Create a dropdown to select the product mode. </summary>
        void select_mode(item_product p)
        {
            var new_mode = (MODE)UnityEditor.EditorGUILayout.EnumPopup("mode", p.mode);
            if (p.mode != new_mode)
            {
                p.mode = new_mode;
                UnityEditor.EditorUtility.SetDirty(p);
            }
        }

        /// <summary> Create a dropdown to select the item produced. </summary>
        void select_item(item_product p)
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
            item_product p = (item_product)target;

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