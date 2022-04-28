using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class nutrition_summary : MonoBehaviour
{
    UnityEngine.UI.Text ui;

    private void OnEnable()
    {
        ui = GetComponent<UnityEngine.UI.Text>();
        if (ui == null)
            return;

        var all_settlers = settler.all_settlers();
        if (all_settlers.Count == 0)
            ui.text = "No settlers.";

        ui.text = "";

        var total_nutrition = new Dictionary<food.GROUP, int>();

        foreach (var s in settler.all_settlers())
            foreach (var fg in food.all_groups)
            {
                if (!total_nutrition.ContainsKey(fg))
                    total_nutrition[fg] = 0;
                total_nutrition[fg] += s.nutrition[fg];
            }

        int max_group_name_length = 0;
        foreach (var fg in food.all_groups)
            max_group_name_length = Mathf.Max(max_group_name_length, food.group_name(fg).Length);

        ui.text += "Average nutrition:\n";

        float conversion = 100f / (all_settlers.Count * byte.MaxValue);
        foreach (var fg in food.all_groups)
            ui.text += food.group_name(fg) + 
                new string(' ', 2 + max_group_name_length - food.group_name(fg).Length) +
                Mathf.RoundToInt(total_nutrition[fg] * conversion) + "%\n";

        var hungry_boi = utils.find_to_min(all_settlers, (s) => -s.hunger_percent());
        ui.text += "\n\nHungriest settler: " + hungry_boi.name + " (" + hungry_boi.hunger_percent() + "% hungry)\n";

        conversion = 100f / byte.MaxValue;
        foreach (var fg in food.all_groups)
            ui.text += food.group_name(fg) +
                new string(' ', 2 + max_group_name_length - food.group_name(fg).Length) +
                Mathf.RoundToInt(hungry_boi.nutrition[fg] * conversion) + "%\n";
    }
}
