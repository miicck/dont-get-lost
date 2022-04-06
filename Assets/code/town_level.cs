using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class town_level : MonoBehaviour
{
    const int BASE_POPULATION_CAP = 5;

    public town_level previous;
    public int added_population_cap = 5;

    public int population_cap =>
        previous == null ? BASE_POPULATION_CAP + added_population_cap :
        previous.population_cap + added_population_cap;

    public bool comes_after(town_level other)
    {
        var previous = this.previous;
        while (previous != null)
        {
            if (previous == other) return true;
            previous = previous.previous;
        }
        return false;
    }

    public string info()
    {
        string ret = name.capitalize();

        ret += "\nPopulation cap: " + population_cap;

        var tech_reqs = GetComponentsInChildren<technology_requirement>();
        if (tech_reqs.Length > 0)
        {
            ret += "\nTechnology requirements:";
            foreach (var t in tech_reqs)
                ret += "\n    [" + (t.satisfied ? "x" : " ") + "] " + t.technology.display_name.capitalize();
        }

        var workshop_reqs = GetComponentsInChildren<workshop_requirement>();
        if (workshop_reqs.Length > 0)
        {
            ret += "\nWorkshop requirements:";
            foreach (var w in workshop_reqs)
                ret += "\n    [" + (w.satisfied(player.current.group) ? "x" : " ") + "] " + w.workshop.display_name;
        }

        return ret;
    }

    public bool unlocked
    {
        get
        {
            foreach (var tr in GetComponentsInChildren<technology_requirement>())
                if (!tr.satisfied)
                    return false;
            return true;
        }
    }

    public static town_level[] ordered
    {
        get
        {
            if (_ordered == null)
            {
                var levels = new List<town_level>(Resources.LoadAll<town_level>("town_levels"));

                levels.Sort((a, b) =>
                {
                    if (b.comes_after(a)) return -1;
                    if (a.comes_after(b)) return 1;
                    return 0;
                });

                _ordered = levels.ToArray();
            }
            return _ordered;
        }
    }
    static town_level[] _ordered;
}
