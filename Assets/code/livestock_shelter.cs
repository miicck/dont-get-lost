using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class livestock_shelter : MonoBehaviour, INonBlueprintable, INonEquipable, IAddsToInspectionText
{
    public character livestock_type;
    public int pasture_per_animal = 2;
    public float animal_produce_time = 30f;

    networked networked_element => GetComponentInChildren<networked>();
    town_path_element path_element => networked_element?.GetComponentInChildren<town_path_element>();
    character[] livestock => networked_element?.GetComponentsInChildren<character>();
    item_output output => GetComponentInChildren<item_output>();
    float last_produce_time = float.MinValue;

    int connected_pasture = 0;
    bool disabled_by_other_shelter;

    private void Update()
    {
        // Only update every 10 frames on auth client
        if (Time.frameCount % 10 != 0) return;
        if (!networked_element.has_authority) return;

        // Work out how much pasture we have
        var room_elms = town_path_element.elements_in_room(path_element.room);
        connected_pasture = 0;
        disabled_by_other_shelter = false;
        foreach (var e in room_elms)
        {
            if (e.name == "pasture")
            {
                connected_pasture += 1;
                continue;
            }

            var other_shelter = e.GetComponentInParent<livestock_shelter>();
            if (other_shelter == null || other_shelter == this) continue;
            if (other_shelter.networked_element.network_id < networked_element.network_id)
            {
                disabled_by_other_shelter = true;
                return;
            }
        }

        // Get the current livestock, spawning any additional animals
        var livestock = new List<character>(this.livestock);
        if (livestock.Count < connected_pasture / pasture_per_animal)
        {
            var new_animal = (character)client.create(
                path_element.transform.position,
                "characters/" + livestock_type.name,
                parent: networked_element);
            livestock.Add(new_animal);
        }

        // Ensure livestock is controlled by the correct thing
        foreach (var l in livestock)
        {
            if (l.controller is livestock_controller) continue;
            l.controller = new livestock_controller(this);
        }

        // Create products
        float dt = Time.realtimeSinceStartup - last_produce_time;
        if (dt > animal_produce_time / livestock.Count)
        {
            last_produce_time = Time.realtimeSinceStartup;
            foreach (var p in GetComponents<product>())
                p.create_in_node(output, track_production: true);
        }
    }

    public string added_inspection_text()
    {
        if (disabled_by_other_shelter)
            return "Disabled (another livestock shelter in room)";
        return connected_pasture + " squares of pasture";
    }

    class livestock_controller : ICharacterController
    {
        livestock_shelter shelter;
        town_path_element current_element;
        town_path_element next_element;
        float sleep_time = 0;
        public livestock_controller(livestock_shelter shelter) { this.shelter = shelter; }

        public void control(character c)
        {
            // This livestock should already be deleted
            // if the shelter has been deleted (as it
            // is parented to it) so just do nothing.
            if (shelter == null) return;

            if (current_element == null)
            {
                // If current element isn't set, set it to the shelter
                current_element = shelter.path_element;
                return;
            }

            if (next_element == null)
            {
                // Select next element as a random neighbour
                var ns = current_element.linked_elements_in_same_room();
                if (ns.Count == 0)
                {
                    // Current element has no neighbours
                    // go back to shelter
                    current_element = null;
                    return;
                }
                next_element = ns[Random.Range(0, ns.Count)];
                if (next_element.name != "pasture") next_element = null;
                return;
            }

            // Sleep
            if (sleep_time > 0)
            {
                sleep_time -= Time.deltaTime;
                return;
            }

            // Walk to next element
            if (utils.move_towards_and_look(c.transform,
                next_element.transform.position,
                Time.deltaTime * c.walk_speed))
            {
                current_element = next_element;
                next_element = null;
                sleep_time = Random.Range(1f, 4f);
            }
        }

        public void draw_gizmos() { }
        public void draw_inspector_gui() { }
        public void on_end_control(character c) { }
        public string inspect_info() { return "Livestock"; }
    }
}
