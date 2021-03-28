using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class task_manager : MonoBehaviour
{
    public UnityEngine.UI.Text title_text;
    settler settler;

    public void set_target(settler settler)
    {
        this.settler = settler;
        title_text.text = "Job priorities for " + settler.name.capitalize();
    }

    public bool open
    {
        get => gameObject.activeInHierarchy;
        set
        {
            Cursor.visible = value;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            gameObject.SetActive(value);
        }
    }

    delegate void callback();
    List<callback> skill_update_funcs = new List<callback>();

    private void Start()
    {
        // Build the job panels
        job_panel job_panel_template = GetComponentInChildren<job_panel>();

        settler.skills.on_change += () =>
        {
            foreach (var c in skill_update_funcs) c();
        };

        foreach (var j in skill.all)
        {
            if (!j.is_visible) continue;
            var job_panel = job_panel_template.inst();
            job_panel.transform.SetParent(job_panel_template.transform.parent);
            job_panel.job_name.text = j.display_name;
            job_panel.job_type = j;
            job_panel.name = j.name;

            skill_update_funcs.Add(() =>
            {
                int level = skill.xp_to_level(settler.skills[j]);
                int xp_current = skill.level_to_xp(level);
                int xp_next = skill.level_to_xp(level + 1);
                int perc_to_next = 100 * (settler.skills[j] - xp_current) / (xp_next - xp_current);

                Vector2 sd = job_panel.skill_progress.rectTransform.sizeDelta;
                job_panel.skill_level_lower.text = "Level " + level;
                job_panel.skill_level_upper.text = "" + (level + 1);
                job_panel.skill_progress.rectTransform.sizeDelta = new Vector2(perc_to_next, sd.y);
            });

            job_panel.increase_priority.onClick.AddListener(() =>
            {
            });

            job_panel.decrease_priority.onClick.AddListener(() =>
            {
            });

            job_panel.enabled_toggle.onValueChanged.AddListener((val) =>
            {
            });
        }

        // Destroy template + update ui to reflect priorities
        job_panel_template.transform.SetParent(null);
        Destroy(job_panel_template.gameObject);
        foreach (var c in skill_update_funcs) c();
    }
}
