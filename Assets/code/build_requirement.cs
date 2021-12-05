using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class build_requirement : tutorial_object
{
    public delegate bool constraint_delegate(building_material material);
    public delegate void confirm_func();

    building_material material;
    int count;
    confirm_func on_complete;
    constraint_delegate constraint;
    string hint = "(In order to build, the object must be equipped. " +
                  "\nSee the bottom right of the screen for building tips.)";

    public UnityEngine.UI.Text text;
    public UnityEngine.UI.Image image;

    HashSet<building_material> buildings = new HashSet<building_material>();

    //##############//
    // STATIC STUFF //
    //##############//

    static Dictionary<string, build_requirement> pending = new Dictionary<string, build_requirement>();

    public static build_requirement create(
        string building, int count, confirm_func on_complete,
        constraint_delegate constraint = null,
        string hint = null)
    {
        // Check that the requested material exists
        var material = Resources.Load<building_material>("items/" + building);
        if (material == null)
        {
            Debug.LogError("Could not find the building " + building);
            return null;
        }

        // Create the build requirement UI
        var br = Resources.Load<build_requirement>("ui/build_requirement").inst();
        br.material = material;
        br.count = count;
        br.on_complete = on_complete;
        br.constraint = constraint;
        br.image.sprite = material.sprite;
        br.text.text = br.hint_text;
        if (hint != null) br.hint = hint;

        // Position it
        var rt = br.GetComponent<RectTransform>();
        rt.SetParent(game.canvas.transform);
        rt.anchoredPosition = Vector2.zero;

        // Register building requirement
        pending[material.name] = br;

        return br;
    }

    private void Update()
    {
        // Update the hint text
        text.text = hint_text;

        if (remaining <= 0)
        {
            on_complete?.Invoke();
            Destroy(gameObject);
            pending.Remove(material.name);
        }
    }

    int remaining
    {
        get
        {
            int ret = count;
            foreach (var b in new List<building_material>(buildings))
            {
                // Check building still exists
                if (b == null)
                {
                    buildings.Remove(b);
                    continue;
                }

                // Check constraint passes for this building
                bool constraint_pass = constraint == null ? true : constraint(b);
                if (!constraint_pass)
                    continue;

                // Decrement remaining count
                if (--ret <= 0)
                    break;
            }

            return ret;
        }
    }

    string hint_text => "Build " + remaining + " x " + material.display_name + "\n" + hint;

    public static void on_build(building_material m)
    {
        // Decrement the remaining counter + call on_built when it hits 0
        if (pending.TryGetValue(m.name, out build_requirement br))
            br.buildings.Add(m);
    }
}

public static class build_requirement_constraints
{
    public static bool is_linked(building_material material)
    {
        town_path_element element = material.GetComponentInChildren<town_path_element>();
        if (element == null)
            Debug.LogError("Tried to check if building material without a path element was linked!");

        foreach (var l in element.links)
            if (l.linked_to_count > 0)
                return true;

        return false;
    }
}
