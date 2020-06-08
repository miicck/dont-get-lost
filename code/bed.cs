using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A bed allows settlers to spawn. </summary>
public class bed : building_material, IInspectable
{
    static HashSet<bed> all_beds;
    
    public static void initialize_beds()
    {
        all_beds = new HashSet<bed>();
    }

    public static bed closest_bed(Vector3 position)
    {
        return utils.find_to_min(all_beds, (b) => (b.transform.position - position).sqrMagnitude);
    }

    HashSet<fixture> fixtures = new HashSet<fixture>();
    settler occupant;
    bool fixtures_dirty = false;

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);

        if (child is settler)
        {
            var s = (settler)child;
            if (occupant != null)
                throw new System.Exception("Bed has multiple occupants!");
            occupant = s;
        }
        else if (child is fixture)
        {
            var f = (fixture)child;
            if (!fixtures.Add(f))
                throw new System.Exception("Tried to add a fixture multiple times!");

            fixtures_dirty = has_authority;
        }
    }

    public override void on_delete_networked_child(networked child)
    {
        if (child is settler)
        {
            var s = (settler)child;
            if (occupant != s)
                throw new System.Exception("Deleted non-existant occupant!");
            occupant = null;
        }
        else if (child is fixture)
        {
            var f = (fixture)child;
            if (!fixtures.Remove(f))
                throw new System.Exception("Removed non-existant fixture!");

            fixtures_dirty = has_authority;
        }
    }

    private void Update()
    {
        if (fixtures_dirty)
        {
            fixtures_dirty = false;
            if (occupant == null)
            {
                // See if we need to spawn an occupant
                foreach (var s in Resources.LoadAll<settler>("settlers"))
                    if (s.requirements_satisfied(fixtures))
                    {
                        client.create(transform.position, "settlers/" + s.name, parent: this);
                        break;
                    }
            }
            else
            {
                // See if we need to un-spawn the occupant
                if (!occupant.requirements_satisfied(fixtures))
                    occupant.delete();
            }
        }
    }

    public override void on_create()
    {
        base.on_create();
        if (!all_beds.Add(this))
            throw new System.Exception("Tried to add a bed multiple times!");
    }

    public override void on_forget(bool deleted)
    {
        base.on_forget(deleted);
        if (!all_beds.Remove(this))
            throw new System.Exception("Tried to remove a non-existant bed!");
    }

    void check_fixtures()
    {
        Debug.Log("checking fixtures...");
    }

    public string inspect_info()
    {
        if (fixtures.Count == 0)
            return "Bed has no fixtures associated.";
        string str = "Bed has the following associated fixtures:\n";
        foreach (var f in fixtures)
            str += " - " + f.display_name + "\n";
        return str;
    }

    public Sprite main_sprite()
    {
        return sprite;
    }

    public Sprite secondary_sprite()
    {
        return null;
    }
}
