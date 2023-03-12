using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_deleter : MonoBehaviour
{
    public UnityEngine.UI.Button delete_button;
    public UnityEngine.UI.Button open_button;

    private void Start()
    {
        open_button.onClick.AddListener(() =>
        {
            // Toggle delete button visibility
            delete_button.gameObject.SetActive(!delete_button.gameObject.activeInHierarchy);
        });

        delete_button.onClick.AddListener(() =>
        {
            var mi = FindObjectOfType<mouse_item>();
            if (mi != null)
                mi.count = 0;
        });
    }

    private void OnEnable()
    {
        // Disable delete button when ui opens, so
        // they don't press it by accident.
        delete_button.gameObject.SetActive(false);
    }
}
