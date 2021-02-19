using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class build_requirement : MonoBehaviour
{
    public delegate void confirm_func();
    confirm_func on_built;

    public UnityEngine.UI.Text text;
    public UnityEngine.UI.Image image;

    //##############//
    // STATIC STUFF //
    //##############//

    static Dictionary<string, build_requirement> pending = new Dictionary<string, build_requirement>();

    public static build_requirement create(string building, confirm_func on_built)
    {
        var b = Resources.Load<building_material>("items/" + building);
        if (b == null)
        {
            Debug.LogError("Could not find the building " + building);
            return null;
        }

        // Destroy previous requirement (if it still exists)
        if (current != null)
            Destroy(current.gameObject);

        var br = Resources.Load<build_requirement>("ui/build_requirement").inst();
        br.image.sprite = b.sprite;
        br.text.text = "Build " + utils.a_or_an(b.display_name) + " " + b.display_name + "\n" +
            "(equip one first, then see the bottom\n" +
            "right of the screen for building tips)";

        var rt = br.GetComponent<RectTransform>();
        rt.SetParent(FindObjectOfType<game>().main_canvas.transform);
        rt.anchoredPosition = Vector2.zero;

        br.on_built = on_built;
        pending[b.name] = br;

        return null;
    }

    static build_requirement current;

    public static void on_build(building_material m)
    {
        if (pending.TryGetValue(m.name, out build_requirement br))
        {
            br.on_built?.Invoke();
            Destroy(br.gameObject);
            pending.Remove(m.name);
        }
    }
}
