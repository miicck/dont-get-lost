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
                int level = settler.skills[j] / settler.XP_PER_LEVEL;
                int perc_to_next = 100 * (settler.skills[j] - level * settler.XP_PER_LEVEL) / settler.XP_PER_LEVEL;
                Vector2 sd = job_panel.skill_progress.rectTransform.sizeDelta;
                job_panel.skill_level_lower.text = "Level " + level;
                job_panel.skill_level_upper.text = "" + (level + 1);
                job_panel.skill_progress.rectTransform.sizeDelta = new Vector2(perc_to_next, sd.y);
            });

            job_panel.increase_priority.onClick.AddListener(() =>
            {
                settler.job_priorities.increase_priority(j);
                update_priorities();
            });

            job_panel.decrease_priority.onClick.AddListener(() =>
            {
                settler.job_priorities.decrease_priority(j);
                update_priorities();
            });

            job_panel.enabled_toggle.onValueChanged.AddListener((val) =>
            {
                settler.job_enabled_state[j] = val;
                update_priorities();
            });

        }

        // Destroy template + update ui to reflect priorities
        job_panel_template.transform.SetParent(null);
        Destroy(job_panel_template.gameObject);
        update_priorities();
        foreach (var c in skill_update_funcs) c();
    }

    public int job_priority(job_panel j)
    {
        return settler.job_priorities.priority(j.job_type);
    }

    public void update_priorities()
    {
        var jps = new List<job_panel>(GetComponentsInChildren<job_panel>());
        jps.Sort((j1, j2) => job_priority(j1).CompareTo(job_priority(j2)));

        foreach (var jp in jps)
        {
            jp.transform.SetAsLastSibling();
            jp.enabled_toggle.isOn = settler.job_enabled_state[jp.job_type];
        }
    }
}
