using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class town_level_menu : MonoBehaviour
{
    public RectTransform town_level_template;
    public RectTransform arrow_template;

    struct level_ui
    {
        public UnityEngine.UI.Image image_to_color;
        public UnityEngine.UI.Text level_info_text;
    }

    Dictionary<town_level, level_ui> level_uis = new Dictionary<town_level, level_ui>();

    private void Start()
    {
        var levels = town_level.ordered;

        for (int i = 0; i < levels.Length; ++i)
        {
            var level = levels[i];

            var level_ui = town_level_template.inst(town_level_template.parent);

            level_uis[level] = new level_ui
            {
                level_info_text = level_ui.Find("text").GetComponent<UnityEngine.UI.Text>(),
                image_to_color = level_ui.GetComponent<UnityEngine.UI.Image>()
            };

            if (i == levels.Length - 1)
                continue; // Don't make an arrow after the last level

            var level_arrow = arrow_template.inst(arrow_template.parent);
        }

        // Destroy the templates
        town_level_template.SetParent(null);
        arrow_template.SetParent(null);
        Destroy(town_level_template.gameObject);
        Destroy(arrow_template.gameObject);

        // Initalize UI
        update_ui();
    }

    void update_ui()
    {
        int group = player.current.group;
        foreach (var kv in level_uis)
        {
            kv.Value.level_info_text.text = kv.Key.info(group);
            kv.Value.image_to_color.color = kv.Key.unlocked(group) ? Color.green : Color.white;
        }
    }

    private void OnEnable()
    {
        update_ui();
    }
}
