using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class town_level : MonoBehaviour
{
    public const int BASE_POPULATION_CAP = 5;

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

    abstract class requirement_set
    {
        public abstract bool empty { get; }
        public abstract string info(int group);
        public abstract bool satisfied(int group);
    }

    class requirement_set<T> : requirement_set where T : Component
    {
        public delegate string name_getter(T t);
        public delegate bool state_getter(T t, int group);

        name_getter name;
        state_getter state;
        T[] components;

        public override bool empty => components.Length == 0;

        public override string info(int group)
        {
            string ret = "";
            foreach (var c in components)
                ret += requirement_line(state(c, group), name(c));
            return ret;
        }

        public override bool satisfied(int group)
        {
            foreach (var c in components)
                if (!state(c, group))
                    return false;
            return true;
        }

        string requirement_line(bool satisfied, string name) => "\n    [" + (satisfied ? "x" : " ") + "] " + name.capitalize();

        public requirement_set(GameObject game_object, name_getter name_getter, state_getter state_getter)
        {
            name = name_getter;
            state = state_getter;
            components = game_object.GetComponentsInChildren<T>();
        }
    }

    Dictionary<string, requirement_set> requirement_sets
    {
        get
        {
            return new Dictionary<string, requirement_set>
            {
                ["Technology requirements"] = new requirement_set<technology_requirement>(
                    gameObject, t => t.technology.display_name, (t, g) => t.satisfied),

                ["Workshop requirements"] = new requirement_set<workshop_requirement>(
                    gameObject, w => w.workshop.display_name, (w, g) => w.satisfied(g)),

                ["Building requirements"] = new requirement_set<connected_building_requirement>(
                    gameObject, b => b.building.display_name, (b, g) => b.satisfied(g))
            };
        }
    }

    public static int current_population_cap
    {
        get
        {
            int cap = BASE_POPULATION_CAP;
            foreach (var l in town_level.ordered)
            {
                if (!l.unlocked(player.current.group))
                    break;
                cap += l.added_population_cap;
            }

            return cap;
        }
    }

    public string info(int group)
    {
        string ret = name.capitalize();

        ret += "\nPopulation cap: " + population_cap;

        foreach (var kv in requirement_sets)
        {
            if (kv.Value.empty)
                continue; // No requirements in this set

            ret += "\n" + kv.Key + ":" + kv.Value.info(group);
        }

        return ret;
    }

    public bool unlocked(int group)
    {
        // Previous level needs to be unlocked
        if (previous != null && !previous.unlocked(group))
            return false;

        // Requirements need to be satisfied
        foreach (var kv in requirement_sets)
            if (!kv.Value.satisfied(group))
                return false;

        return true;
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
