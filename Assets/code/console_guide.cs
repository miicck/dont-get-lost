using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class console_guide : MonoBehaviour
{
    public GameObject example_entry;

    private void Start()
    {
        foreach (var c in console.commands)
        {
            var entry = example_entry.inst();
            entry.transform.SetParent(example_entry.transform.parent);

            UnityEngine.UI.Text name = null;
            UnityEngine.UI.Text desc = null;

            foreach (var t in entry.GetComponentsInChildren<UnityEngine.UI.Text>())
            {
                if (t.name.Contains("command")) name = t;
                else if (t.name.Contains("description")) desc = t;
            }

            name.text = c.Key;
            desc.text = c.Value.description + "\n<i>" + c.Value.usage_example + "</i>\n";
        }
    }
}
