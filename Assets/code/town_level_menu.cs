using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class town_level_menu : MonoBehaviour
{
    public RectTransform town_level_template;
    public RectTransform arrow_template;

    private void Start()
    {
        var levels = town_level.ordered;

        for (int i = 0; i < levels.Length; ++i)
        {
            var level = levels[i];

            var level_ui = town_level_template.inst();
            level_ui.SetParent(town_level_template.parent);

            var level_text = level_ui.Find("text").GetComponent<UnityEngine.UI.Text>();
            level_text.text = level.info();

            level_ui.GetComponent<UnityEngine.UI.Image>().color = level.unlocked ? Color.green : Color.white;

            if (i == levels.Length - 1)
                continue; // Don't make an arrow after the last level

            var level_arrow = arrow_template.inst();
            level_arrow.transform.SetParent(arrow_template.parent);
        }

        // Destroy the templates
        town_level_template.SetParent(null);
        arrow_template.SetParent(null);
        Destroy(town_level_template.gameObject);
        Destroy(arrow_template.gameObject);
    }

    private void Update()
    {

    }
}
