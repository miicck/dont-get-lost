using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class technology : MonoBehaviour
{
    public List<technology> depends_on;
    public Sprite sprite;
    public string description;

    public virtual string info()
    {
        string ret = description;
        if (increased_population_cap > 0)
            ret += "\nIncreses population cap by " + increased_population_cap;

        foreach (var i in Resources.LoadAll<item>("items"))
            if (i.GetComponent<technology_requirement>()?.technology == this)
                ret += "\nUnlocks " + i.display_name;

        return ret;
    }

    public int increased_population_cap
    {
        get
        {
            var ipc = GetComponent<increases_population_cap>();
            return ipc == null ? 0 : ipc.increases_population_cap_by;
        }
    }

    public bool complete => tech_tree.research_complete(name);

    public bool prerequisites_complete
    {
        get
        {
            foreach (var t in depends_on)
                if (!t.complete)
                    return false;
            return true;
        }
    }

    public bool materials_available
    {
        get
        {
            foreach (var material in GetComponentsInChildren<research_material_ingredient>())
                if (tech_tree.research_materials_count(material.material) < material.count)
                    return false;
            return true;
        }
    }

    public HashSet<technology> depends_on_set
    {
        get
        {
            if (_depends_on_set == null)
                _depends_on_set = new HashSet<technology>(depends_on);
            return _depends_on_set;
        }
    }
    HashSet<technology> _depends_on_set;

    public bool linked_to(technology other) => depends_on_set.Contains(other) || other.depends_on_set.Contains(this);

    //##############//
    // STATIC STUFF //
    //##############//

    public static technology[] all => Resources.LoadAll<technology>("technologies");

    public static bool is_valid_name(string name)
    {
        foreach (var t in all)
            if (t.name == name)
                return true;
        return false;
    }

    public static technology load(string name)
    {
        foreach (var t in all)
            if (t.name == name)
                return t;
        return null;
    }
}
