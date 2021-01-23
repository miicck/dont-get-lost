using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class skill_panel : MonoBehaviour
{
    public UnityEngine.UI.Text name_text;
    public UnityEngine.UI.Text level_text;
    public UnityEngine.UI.Text progress_text;
    public UnityEngine.UI.Image progress_bar_foreground;
    public settler.SKILL skill;

    public int current_xp
    {
        get => _current_xp;
        set
        {
            // Work out level from xp
            int level = Mathf.FloorToInt(value / settler.XP_PER_LEVEL);
            level_text.text = level.ToString();
            int xp_this_level = value - level * settler.XP_PER_LEVEL;
            progress_text.text = xp_this_level + "/" + settler.XP_PER_LEVEL;

            // Set the progress bar to reflect xp this level
            var rt = progress_bar_foreground.GetComponent<RectTransform>();
            var parent = rt.parent.GetComponent<RectTransform>();
            Vector2 offset_max = rt.offsetMax;
            offset_max.x = -parent.sizeDelta.x * (settler.XP_PER_LEVEL - xp_this_level) / settler.XP_PER_LEVEL;
            rt.offsetMax = offset_max;

            _current_xp = value;
        }
    }
    int _current_xp = 0;
}
