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
        // Build the task manager
        job_panel template = GetComponentInChildren<job_panel>();

        foreach (var j in job_type.all)
        {
            if (!j.can_set_priority) continue;
            var job_panel = template.inst();
            job_panel.transform.SetParent(template.transform.parent);
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

        // Destroy templates
        template.transform.SetParent(null);
        Destroy(template.gameObject);
        update_priorities();
    }

    public int job_priority(job_panel j)
    {
        if (j == null || j.job_type == null) return 0;
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
