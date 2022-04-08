using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class job_manager : MonoBehaviour, ISimpleMenuObject
{
    public RectTransform header_skill_template;
    public RectTransform row_skill_template;
    public RectTransform row_template;

    public UnityEngine.UI.ScrollRect header_rect;
    public UnityEngine.UI.ScrollRect table_rect;

    void setup_skills()
    {
        // Already done?
        if (header_skill_template == null) return;

        foreach (var s in skill.all)
        {
            if (!s.is_visible) continue;

            // Create the header for this skill
            var h = header_skill_template.inst();
            h.SetParent(header_skill_template.parent);
            h.GetComponentInChildren<UnityEngine.UI.Text>().text = s.display_name.capitalize();

            // Create the skills in the row template
            var r = row_skill_template.inst();

            foreach (var b in r.GetComponentsInChildren<UnityEngine.UI.Button>())
                b.gameObject.AddComponent<button_mouse_text>();

            r.SetParent(row_skill_template.parent);
            r.name = s.name;
        }

        // The skills are built and will not need
        // changing, delete corresponding templates
        // (disable first so changes take place immediately)
        row_skill_template.gameObject.SetActive(false);
        header_skill_template.gameObject.SetActive(false);
        Destroy(header_skill_template.gameObject);
        Destroy(row_skill_template.gameObject);
        header_skill_template = null;
        row_skill_template = null;

        // Only disable the row template as it
        // is used in the refresh() function
        row_template.gameObject.SetActive(false);
    }

    class button_mouse_text : MonoBehaviour, IMouseTextUI
    {
        public string text = "";
        public string mouse_ui_text() => this.text;
    }

    public void refresh()
    {
        setup_skills();

        // Delete all old rows
        foreach (RectTransform row in row_template.parent)
            if (row != row_template)
                Destroy(row.gameObject);

        // Reactivate the row template for copying
        row_template.gameObject.SetActive(true);

        // Create a row for each settler
        foreach (var s in settler.all_settlers())
        {
            var r = row_template.inst();
            r.SetParent(row_template.parent);

            var name_button = r.Find("name_button").GetComponent<UnityEngine.UI.Button>();
            name_button.onClick.AddListener(() =>
            {
                // Ping the settler if you click their name
                if (s == null) return;
                client.create(s.transform.position + s.transform.up * s.height, "misc/map_ping", parent: s);
            });
            name_button.GetComponentInChildren<UnityEngine.UI.Text>().text = s.name.capitalize();

            foreach (var sk in skill.all)
            {
                if (!sk.is_visible) continue;

                var tra = r.Find("skills").Find(sk.name);
                if (tra == null) Debug.LogError("Could not find skill entry for " + sk);
                var but = tra.GetComponentInChildren<UnityEngine.UI.Button>();
                var txt = but.GetComponentInChildren<UnityEngine.UI.Text>();
                txt.text = s.skills[sk].level.ToString();
                but.image.color = skill.priority_color(s.job_priorities[sk]);

                but.GetComponent<button_mouse_text>().text = "xp to next: " + s.skills[sk].xp_to_next;

                but.onClick.AddListener(() =>
                {
                    if (s == null) return;
                    s.job_priorities[sk] = skill.cycle_priority(s.job_priorities[sk]);
                    refresh();
                });
            }
        }

        // Deactivate the row template again
        row_template.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Keep the header alligned to the table
        header_rect.horizontalNormalizedPosition = table_rect.horizontalNormalizedPosition;
    }

    // ISimpleMenuObject
    public void on_menu_open() => refresh();
}
