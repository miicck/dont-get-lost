using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary> A biome modifier that does nothing. </summary>
public class no_modifier : biome_modifier
{
    public override void modify(biome b) { }
}

[biome_mod_info(generation_enabled: false)]
public class reclaimed_city : biome_modifier
{
    int CITY_BLOCK_SIZE = 16;

    public override void modify(biome b)
    {
        bool[,] is_building = new bool[
             biome.SIZE / CITY_BLOCK_SIZE,
             biome.SIZE / CITY_BLOCK_SIZE];

        for (int i = 0; i < biome.SIZE / CITY_BLOCK_SIZE; ++i)
            for (int j = 0; j < biome.SIZE / CITY_BLOCK_SIZE; ++j)
                is_building[i, j] = b.random.range(0, 3) == 0;

        for (int i = 0; i < biome.SIZE; ++i)
            for (int j = 0; j < biome.SIZE; ++j)
            {
                var p = b.grid[i, j];

                // Check what kind of city block we're in
                if (is_building[i / CITY_BLOCK_SIZE, j / CITY_BLOCK_SIZE])
                {
                    // Clear surroundings 
                    p.object_to_generate = null;

                    // If were at the centre of a block, generate a building
                    if (i % CITY_BLOCK_SIZE == CITY_BLOCK_SIZE / 2 &&
                        j % CITY_BLOCK_SIZE == CITY_BLOCK_SIZE / 2)
                    {
                        switch (b.random.range(0, 4))
                        {
                            case 0:
                                p.object_to_generate = world_object.load("tower_block");
                                break;

                            case 1:
                                p.object_to_generate = world_object.load("tower_block_collapsed");
                                break;

                            case 2:
                                p.object_to_generate = world_object.load("department_store");
                                break;

                            case 3:
                                p.object_to_generate = world_object.load("building_shell");
                                break;
                        }
                    }
                }
            }
    }
}

[biome_mod_info(generation_enabled: false)]
public class craters : biome_modifier
{
    const int BURNT_BIT_SIZE = 32;

    void add_burnt_bit(biome b)
    {
        int x = b.random.range(BURNT_BIT_SIZE / 2, biome.SIZE - BURNT_BIT_SIZE / 2);
        int z = b.random.range(BURNT_BIT_SIZE / 2, biome.SIZE - BURNT_BIT_SIZE / 2);

        for (int dx = -BURNT_BIT_SIZE / 2; dx < BURNT_BIT_SIZE / 2; ++dx)
            for (int dz = -BURNT_BIT_SIZE / 2; dz < BURNT_BIT_SIZE / 2; ++dz)
            {
                var p = b.grid[x + dx, z + dz];

                // Remove objects
                p.object_to_generate = null;

                // Lower terrain
                float im = procmath.maps.maximum_in_middle(dx, -BURNT_BIT_SIZE / 2, BURNT_BIT_SIZE / 2);
                float jm = procmath.maps.maximum_in_middle(dz, -BURNT_BIT_SIZE / 2, BURNT_BIT_SIZE / 2);
                float amt = im * jm;
                p.altitude -= amt * 8f;

                p.terrain_color = p.terrain_color * (1f - amt) + Color.black * amt;

                if (Random.Range(0, 50) == 0)
                    p.object_to_generate = world_object.load("zombie_spawn_point");
            }

        b.grid[x, z].object_to_generate = world_object.load("random_wreckage");
    }

    public override void modify(biome b)
    {
        for (int i = 0; i < 8; ++i)
            add_burnt_bit(b);
    }
}

[biome_mod_info(generation_enabled: false)]
public class cave_system : biome_modifier
{
    const int MAX_FAILS = 100;
    const float CAVE_START_ALT = world.UNDERGROUND_ROOF - 100f;
    const float CAVE_END_MAX_ALT = world.UNDERGROUND_ROOF - 50f;

    HashSet<GameObject> open_sections = new HashSet<GameObject>();
    HashSet<GameObject> all_sections = new HashSet<GameObject>();

    public override void modify(biome b)
    {
        // Get the list of sections + create the seed section
        var sections = Resources.LoadAll<GameObject>("cave_sections");
        var seed = sections[b.random.range(0, sections.Length)].inst();
        seed.transform.position = b.transform.position + new Vector3(
            biome.SIZE / 2, CAVE_START_ALT, biome.SIZE / 2);
        open_sections.Add(seed);
        all_sections.Add(seed);

        // Get the end section for terminations, if the network gets too big
        var end_section = Resources.Load<GameObject>("cave_sections/end_section");

        int failed_placements = 0;

        while (open_sections.Count > 0)
        {
            // Select the first open section
            var section = open_sections.First();
            var links = section.GetComponentsInChildren<cave_link_point>().Where((l) => l.linked_to == null);
            if (links.Count() == 0)
            {
                // Close this section
                open_sections.Remove(section);
                continue;
            }

            // Select the link to expand
            var link = links.ElementAt(b.random.range(0, links.Count()));
            if (link.linked_to != null)
                throw new System.Exception("This shouldn't happen!");

            // Attempt to find a section to link to it
            while (true)
            {
                GameObject new_section;

                Vector3 delta = link.transform.position - b.transform.position;
                if (delta.x < 0 || delta.z < 0 || delta.x > biome.SIZE || delta.z > biome.SIZE ||
                    link.transform.position.y > CAVE_END_MAX_ALT)
                    // Gone out of range, make this an end
                    new_section = end_section.inst();
                else
                    // Choose a new section at random
                    new_section = sections[b.random.range(0, sections.Length)].inst();

                var new_links = new_section.GetComponentsInChildren<cave_link_point>();
                if (new_links.Length == 0)
                    throw new System.Exception("Found a section with no links!");

                // Cycle through links in the new section
                bool placed = false;
                for (int i = 0; i < new_links.Length; ++i)
                {
                    // Allign the links
                    var link_to = new_links[i];
                    allign_sections(new_section, link_to, link);

                    if (link_to.linked_to != null)
                        throw new System.Exception("This shouldn't happen");

                    // Test for overlap
                    bool overlaps = false;
                    foreach (var s in all_sections)
                    {
                        var b1 = s.GetComponent<BoxCollider>().bounds;
                        var b2 = new_section.GetComponent<BoxCollider>().bounds;
                        b1.center += s.transform.position;
                        b2.center += new_section.transform.position;
                        b1.size -= Vector3.one * 0.1f;
                        b2.size -= Vector3.one * 0.1f;
                        if (b1.Intersects(b2))
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (!overlaps)
                    {
                        // Place the new section
                        placed = true;
                        link.linked_to = link_to;

                        // Add new section to open set
                        open_sections.Add(new_section);
                        all_sections.Add(new_section);
                        break;
                    }
                }

                if (placed) break;
                else
                {
                    // Destroy section that couldn't be placed
                    Object.Destroy(new_section.gameObject);
                    if (++failed_placements > MAX_FAILS)
                        break;
                }
            }

            if (failed_placements > MAX_FAILS)
                break;
        }

        // Cap off all remaining open links
        foreach (var section in open_sections)
        {
            var links = section.GetComponentsInChildren<cave_link_point>().Where((l) => l.linked_to == null);
            foreach (var l in links)
            {
                var cap = end_section.inst();
                var cap_link = cap.GetComponentInChildren<cave_link_point>();
                allign_sections(cap, cap_link, l);
                l.linked_to = cap_link;
                all_sections.Add(cap);
            }
        }

        // Tidy up the hierarcy
        GameObject container = new GameObject("cave_system");
        container.transform.SetParent(b.transform);
        container.transform.localPosition = Vector3.zero;
        foreach (var s in all_sections)
        {
            Object.Destroy(s.GetComponent<BoxCollider>());
            s.transform.SetParent(container.transform);
        }

        // Free memory
        all_sections = null;
        open_sections = null;

        foreach (var link in container.GetComponentsInChildren<cave_link_point>())
            if (link.link_to_surface)
            {
                b.get_grid_coords(link.transform.position, out int i, out int j);
                b.grid[i, j].object_to_generate = world_object.load("cave_system_link");
            }
    }

    void allign_sections(GameObject section, cave_link_point link_in_section, cave_link_point external_link)
    {
        section.transform.rotation =
         Quaternion.Euler(0, 180, 0) *
         Quaternion.Inverse(link_in_section.transform.localRotation) *
         external_link.transform.rotation;
        section.transform.position += external_link.transform.position -
            link_in_section.transform.position;
    }
}

[biome_mod_info(generation_enabled: false)]
public class traders : biome_modifier
{
    const int CLEAR_RANGE = 8;

    public override void modify(biome b)
    {
        for (int n = 0; n < 8; ++n)
            add_trader(b,
                b.random.range(0, biome.SIZE),
                b.random.range(0, biome.SIZE));
    }

    void add_trader(biome b, int i, int j)
    {
        if (i >= biome.SIZE - CLEAR_RANGE) return;
        if (j >= biome.SIZE - CLEAR_RANGE) return;

        for (int x = i; x < i + CLEAR_RANGE; ++x)
            for (int y = j; y < j + CLEAR_RANGE; ++y)
                b.grid[x, y].object_to_generate = null;

        b.grid[i + CLEAR_RANGE / 2, j + CLEAR_RANGE / 2].object_to_generate = world_object.load("traders_hut");
    }
}