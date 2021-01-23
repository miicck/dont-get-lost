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

    private void Start()
    {
        // Build the job panels
        job_panel job_panel_template = GetComponentInChildren<job_panel>();

        foreach (var j in job_type.all)
        {
            if (!j.can_set_priority) continue;
            var job_panel = job_panel_template.inst();
            job_panel.transform.SetParent(job_panel_template.transform.parent);
            job_panel.job_name.text = j.display_name;
            job_panel.job_type = j;
            job_panel.name = j.name;

            if (j.relevant_skills.Count == 0)
            {
                job_panel.relevant_skills.text = "No relevant skills";
            }
            else
            {
                string rs = "Relevant skills:\n";
                foreach (var s in j.relevant_skills)
                    rs += settler.skill_name(s) + ",";
                rs = rs.Remove(rs.Length - 1);
                job_panel.relevant_skills.text = rs;
            }

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

        // Build the skill panels
        skill_panel skill_panel_template = GetComponentInChildren<skill_panel>();
        settler.skills.on_change += update_skills;

        foreach (var s in settler.all_skills)
        {
            settler.SKILL stmp = s;
            var skill_panel = skill_panel_template.inst();
            skill_panel.transform.SetParent(skill_panel_template.transform.parent);
            skill_panel.skill = stmp;
            skill_panel.name = settler.skill_name(stmp);
            skill_panel.name_text.text = settler.skill_name(stmp);
            skill_panel.current_xp = settler.skills[stmp];
        }

        // Destroy template + update ui to reflect current xps
        skill_panel_template.transform.SetParent(null);
        Destroy(skill_panel_template.gameObject);
        update_skills();
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

    public void update_skills()
    {
        // Update xp for each skill
        var sps = new List<skill_panel>(GetComponentsInChildren<skill_panel>());
        foreach (var s in sps)
            s.current_xp = settler.skills[s.skill];

        // Show skills in decending xp order
        sps.Sort((s1, s2) => s2.current_xp.CompareTo(s1.current_xp));
        foreach (var sp in sps)
            sp.transform.SetAsLastSibling();
    }
}
