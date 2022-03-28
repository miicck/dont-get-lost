using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class research_material_ingredient : ingredient
{
    public research_material material;
    public int count;

    public override float average_value() => 0f;

    bool find(IItemCollection i, ref Dictionary<string, int> in_use, out int found)
    {
        found = Mathf.Min(tech_tree.research_materials_count(material), count);

        if (in_use.ContainsKey(material.name)) in_use[material.name] += found;
        else if (found > 0) in_use[material.name] = found;

        return found >= count;
    }

    public override bool find(IItemCollection i, ref Dictionary<string, int> in_use)
    {
        return find(i, ref in_use, out int ignored);
    }

    public override string satisfaction_string(IItemCollection i, ref Dictionary<string, int> in_use)
    {
        find(i, ref in_use, out int found);
        return found + "/" + count + " " + material.name;
    }

    public override string str()
    {
        return count + " " + material.name;
    }
}
