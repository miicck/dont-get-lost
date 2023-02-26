using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class keybinds_menu : MonoBehaviour
{
    void setup_buttons()
    {
        const int ENTRY_TEMPLATE_CHILD = 2;

        var entry_template = transform.GetChild(ENTRY_TEMPLATE_CHILD);
        entry_template.gameObject.SetActive(true);

        // Destroy old buttons
        while (transform.childCount > ENTRY_TEMPLATE_CHILD + 1)
        {
            var to_destoy = transform.GetChild(ENTRY_TEMPLATE_CHILD + 1);
            to_destoy.SetParent(null);
            Destroy(to_destoy.gameObject);
        }

        foreach (controls.BIND b in System.Enum.GetValues(typeof(controls.BIND)))
        {
            if (!controls.is_reconfigurable(b))
                continue; // This keybind can't be configured

            // Create new entries
            var entry = entry_template.inst(entry_template.parent);
            entry.name = b.ToString();

            var entry_name = entry.GetChild(0).GetComponentInChildren<UnityEngine.UI.Text>();
            entry_name.text = b.ToString();

            var entry_button = entry.GetChild(1).GetComponentInChildren<UnityEngine.UI.Button>();
            var entry_button_text = entry_button.GetComponentInChildren<UnityEngine.UI.Text>();
            entry_button_text.text = controls.bind_name(b);
            entry_button.onClick.RemoveAllListeners();
            entry_button.onClick.AddListener(() =>
            {
                entry_button_text.text = "...";
                controls.capture_next(b, setup_buttons);
            });
        }

        // Disable entry template
        entry_template.gameObject.SetActive(false);
    }

    private void Start()
    {
        setup_buttons();
    }

    public void restore_defaults()
    {
        controls.restore_defaults();
        setup_buttons();
    }
}
