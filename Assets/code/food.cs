using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Add to an object to describe it's nutritional values. </summary>
public class food : MonoBehaviour
{
    [SerializeField]
    byte[] food_group_values = new byte[total_groups];

    public byte food_value(GROUP g) { return food_group_values[(int)g]; }

    public byte metabolic_value()
    {
        byte val = 0;
        foreach (var g in all_groups)
            if (can_metabolize(g))
                if (food_group_values[(int)g] > val)
                    val = food_group_values[(int)g];
        return val;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    public enum GROUP
    {
        PROTIEN,
        CARBOHYDRATES,
        VITAMINS,
        FIBRE,
        FATS
    }

    public static bool can_metabolize(GROUP g)
    {
        switch (g)
        {
            case GROUP.VITAMINS: return false;
            case GROUP.FIBRE: return false;
            default: return true;
        }
    }

    public static GROUP[] all_groups => (GROUP[])System.Enum.GetValues(typeof(GROUP));
    public static int total_groups = System.Enum.GetNames(typeof(GROUP)).Length;
    public static string group_name(GROUP g) { return g.ToString().ToLower(); }

    //########//
    // EDITOR //
    //########//

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(food))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var food = (food)target;
            var names = System.Enum.GetNames(typeof(GROUP));

            for (int i = 0; i < total_groups; ++i)
            {
                var value_before = food.food_group_values[i];

                food.food_group_values[i] = (byte)Mathf.Clamp(
                    UnityEditor.EditorGUILayout.IntField(names[i],
                    food.food_group_values[i], new GUILayoutOption[0]),
                    0, byte.MaxValue);

                if (value_before != food.food_group_values[i])
                    UnityEditor.EditorUtility.SetDirty(food);
            }
        }
    }
#endif
}
