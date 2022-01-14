using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class technology : MonoBehaviour
{
    public List<technology> depends_on;
    public Sprite sprite;

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
