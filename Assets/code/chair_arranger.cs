using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class chair_arranger : MonoBehaviour
{
    /// <summary> The object who's inventory contains chairs to arrange. </summary>
    public building_with_inventory chair_inventory;

    /// <summary> The locations that the chairs are placed. </summary>
    public List<Transform> chair_spots;

    /// <summary> Get the chairs that are currently arranged. </summary>
    public chair[] chairs
    {
        get
        {
            var ret = new List<chair>();
            foreach (var t in chair_spots)
            {
                var c = t.GetComponentInChildren<chair>();
                if (c == null) continue;
                ret.Add(c);
            }
            return ret.ToArray();
        }
    }

    private void Start()
    {
        chair_inventory.add_on_set_inventory_listener(() =>
        {
            chair_inventory.inventory.add_on_change_listener(() =>
            {
                // Destroy previous chairs
                foreach (var c in chairs)
                    Destroy(c.gameObject);

                // Identify new chairs
                List<item> new_chairs = new List<item>();
                foreach (var kv in chair_inventory.inventory.contents())
                {
                    var c = kv.Key.GetComponent<chair>();
                    if (c == null) continue;

                    for (int i = 0; i < kv.Value && new_chairs.Count < chair_spots.Count; ++i)
                        new_chairs.Add(kv.Key);
                }

                // Create the new chairs
                for (int i = 0; i < new_chairs.Count && i < chair_spots.Count; ++i)
                {
                    var s = chair_spots[i];
                    var c = item.create(new_chairs[i].name, s.transform.position, s.transform.rotation);
                    c.transform.SetParent(s);

                    foreach (var inter in c.GetComponentsInChildren<IPlayerInteractable>())
                    {
                        var comp = inter as Component;
                        if (comp == null) continue;
                        Destroy(comp);
                    }
                }
            });

            // Trigger on change the first time the inventory loads
            chair_inventory.inventory.invoke_on_change();
        });
    }
}