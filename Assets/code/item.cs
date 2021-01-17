using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface INonEquipable { }
public interface INonEquipableCallback : INonEquipable { void on_equip_callback(player player); }
public interface INonLogistical { }

public class item : networked, IPlayerInteractable
{
    public const float LOGISTICS_SIZE = 0.3f;

    static item()
    {
        tips.add("To see if you can eat an item, hover over it and press " +
                 controls.bind_name(controls.BIND.INSPECT) + " to check it's food value. " +
                 "Equip it in your quckbar and left click to eat.");
    }

    //###########//
    // VARIABLES //
    //###########//

    public Sprite sprite; // The sprite represeting this item in inventories etc
    public string plural;
    public int value;
    public int fuel_value = 0;
    public float logistics_scale = 1f; // How much to scale the item by when it is in the logistics network
    public bool is_equpped => GetComponentInParent<player>() != null;

    public food food_values => GetComponent<food>();

    void make_logistics_version()
    {
        if (!is_client_side)
            throw new System.Exception("Can only make client side items into the logstics version!");

        is_logistics_version = true;
        transform.localScale *= logistics_scale;

        // Remove components that are incompatible with the logistics version
        foreach (var c in GetComponentsInChildren<Component>())
            if (c is INonLogistical)
                Destroy(c);
    }

    public bool is_logistics_version { get; private set; }

    public string display_name
    {
        get => name.Replace('_', ' ');
    }

    public string singular_or_plural(int count)
    {
        if (count == 1) return display_name;
        return plural;
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public virtual player_interaction[] player_interactions()
    {
        return new player_interaction[]
        {
            new pick_up_interaction(this),
            new select_matching_interaction(this),
            new player_inspectable(transform)
            {
                text = () => item_quantity_info(this, 1),
                sprite = () => sprite
            }
        };
    }

    class pick_up_interaction : player_interaction
    {
        item item;
        public pick_up_interaction(item item) { this.item = item; }

        public override controls.BIND keybind => controls.BIND.PICK_UP_ITEM;

        public override bool start_interaction(player player)
        {
            item.pick_up();
            return true;
        }

        public override string context_tip()
        {
            return "pick up " + item.display_name;
        }
    }

    protected class select_matching_interaction : player_interaction
    {
        item item;
        public select_matching_interaction(item item) { this.item = item; }

        public override controls.BIND keybind => controls.BIND.SELECT_ITEM_FROM_WORLD;

        public override bool start_interaction(player player)
        {
            player.current.equip_matching(item);
            return true;
        }

        public override string context_tip()
        {
            return "equip matching objects from inventory";
        }
    }

    //############//
    // PLAYER USE //
    //############//

    player_interaction[] interactions;

    public virtual player_interaction[] item_uses()
    {
        if (interactions == null)
        {
            interactions = new player_interaction[]
            {
                new eat_interaction(this),
                new place_on_gutter(this)
            };
        }
        return interactions;
    }

    abstract class player_item_interaction : player_interaction
    {
        protected item item { get; private set; }
        public player_item_interaction(item item) { this.item = item; }
    }

    class eat_interaction : player_item_interaction
    {
        public eat_interaction(item i) : base(i) { }

        public override bool is_possible() { return item.food_values != null; }
        public override controls.BIND keybind => controls.BIND.USE_ITEM;

        public override string context_tip()
        {
            return "eat " + item.display_name;
        }

        public override bool start_interaction(player player)
        {
            if (player.inventory.remove(item, 1))
                player.modify_hunger(item.food_values.metabolic_value());
            return true;
        }
    }

    class place_on_gutter : player_item_interaction
    {
        public place_on_gutter(item i) : base(i) { }

        public override controls.BIND keybind => controls.BIND.PLACE_ON_GUTTER;

        public override string context_tip()
        {
            return "place " + item.display_name + " on gutter";
        }

        public override bool start_interaction(player player)
        {
            var ray = player.camera_ray(player.INTERACTION_RANGE, out float dis);
            var gutter = utils.raycast_for_closest<item_gutter>(ray, out RaycastHit hit, dis);
            if (gutter == null) return true;
            if (player.inventory.remove(item, 1))
                gutter.add_item(create(item.name, hit.point, Quaternion.identity, logistics_version: true));
            return true;
        }
    }

    /// <summary> Called when this item is equipped.</summary>
    public virtual void on_equip(player player)
    {
        // Remove all colliders
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        // Make it invisible.
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = false;

        // Destroy non-equippable things
        foreach (Component eq in GetComponentsInChildren<INonEquipable>())
        {
            if (eq is INonEquipableCallback)
                ((INonEquipableCallback)eq).on_equip_callback(player);
            Destroy(eq);
        }
    }

    /// <summary> Called when this item is unequipped. <paramref name="local_player"/> = false
    /// iff this item is being unequipped by a remote player. </summary>
    public virtual void on_unequip(player player) { }

    public virtual Dictionary<string, int> add_to_inventory_on_pickup()
    {
        var ret = new Dictionary<string, int>();
        ret[name] = 1;
        return ret;
    }

    public void pick_up(bool register_undo = false)
    {
        if (this == null) return;

        if (!can_pick_up(out string message))
        {
            popup_message.create("Cannot pick up " + display_name + ": " + message);
            return;
        }

        var undo = pickup_undo();

        // Delete the object on the network / add it to
        // inventory only if succesfully deleted on the
        // server. This stops two clients from simultaneously
        // deleting an object to duplicate it.
        var to_pickup = add_to_inventory_on_pickup();
        delete(() =>
        {
            // Add the products from pickup into inventory
            foreach (var kv in to_pickup)
                player.current.inventory.add(kv.Key, kv.Value);

            if (register_undo)
                undo_manager.register_undo_level(undo);
        });
    }

    public undo_manager.undo_action pickup_undo()
    {
        if (this == null) return null; // Destroyed

        // Copies for lambda
        var pickup_items = add_to_inventory_on_pickup();
        string name_copy = string.Copy(name);
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;
        networked parent = transform.parent?.GetComponent<networked>();

        return () =>
        {
            // Check we still have all of the products
            foreach (var kv in pickup_items)
                if (!player.current.inventory.contains(kv.Key, kv.Value))
                    return null;

            // Remove all of the products
            foreach (var kv in pickup_items)
                if (!player.current.inventory.remove(kv.Key, kv.Value))
                    throw new System.Exception("Tried to remove non-existant item!");

            // Recreate the building
            var created = create(name_copy, pos, rot, networked: true, parent);

            // Return the redo function
            return () =>
            {
                // Redo the pickup, and return the redo-undo (yo, what)
                created.pick_up();
                return created.pickup_undo();
            };
        };
    }

    protected virtual bool can_pick_up(out string message)
    {
        message = null;
        return true;
    }

    //############//
    // NETWORKING //
    //############//

    public networked_variables.net_quaternion networked_rotation;

    public override void on_init_network_variables()
    {
        // Create newtorked variables
        networked_rotation = new networked_variables.net_quaternion();
        transform.rotation = Quaternion.identity;
        networked_rotation.on_change = () => transform.rotation = networked_rotation.value;
    }

    public override void on_create()
    {
        // Initialize networked variables
        networked_rotation.value = transform.rotation;
    }

    //################//
    // STATIC METHODS //
    //################//

    /// <summary> Create an item. </summary>
    public static item create(string name,
        Vector3 position, Quaternion rotation,
        bool networked = false,
        networked network_parent = null,
        bool register_undo = false,
        bool logistics_version = false)
    {
        item item = null;

        if (networked)
        {
            // Create a networked version of the chosen item
            item = (item)client.create(position, "items/" + name,
                rotation: rotation, parent: network_parent);

            if (register_undo)
                undo_manager.register_undo_level(() =>
                {

                    if (item == null) return null;
                    var redo = item.pickup_undo();
                    item.pick_up();
                    return redo;
                });

        }
        else
        {
            // Create a client-side only version of the item
            item = Resources.Load<item>("items/" + name);
            if (item == null)
                throw new System.Exception("Could not find the item: " + name);
            item = item.inst();
            item.is_client_side = true;
            item.transform.position = position;
            item.transform.rotation = rotation;
            item.transform.SetParent(network_parent == null ? null : network_parent.transform);

            if (logistics_version)
                item.make_logistics_version();
        }

        return item;
    }

    public static string item_quantity_info(item item, int quantity)
    {
        if (item == null || quantity == 0)
            return "No item.";

        // Title
        string info = (quantity < 2 ? item.display_name :
            (utils.int_to_comma_string(quantity) + " " + item.plural)) + "\n";

        // Value
        if (quantity > 1)
            info += "  Value : " + (item.value * quantity).qs() + " (" + item.value.qs() + " each)\n";
        else
            info += "  Value : " + item.value.qs() + "\n";

        // Tool type + quality
        if (item is tool)
        {
            var t = (tool)item;
            info += "  Tool type : " + tool.type_to_name(t.type) + "\n";
            info += "  Quality : " + tool.quality_to_name(t.quality) + "\n";
        }

        // Melee weapon info
        if (item is melee_weapon)
        {
            var m = (melee_weapon)item;
            info += "  Melee damage : " + m.damage + "\n";
        }

        // Can this item be built with
        if (item is building_material)
            info += "  Can be used for building\n";

        // Fuel value
        if (item.fuel_value > 0)
        {
            if (quantity > 1)
                info += "  Fuel value : " + (item.fuel_value * quantity).qs() + " (" + item.fuel_value.qs() + " each)\n";
            else
                info += "  Fuel value : " + item.fuel_value.qs() + "\n";
        }

        // Food value
        if (item.food_values != null)
            info += "Food values: " + item.food_values.shorthand_notation() + "\n";

        return utils.allign_colons(info);
    }

    int suggested_value(out recipe recipe_determining_value)
    {
        recipe_determining_value = null;

        // Value is fixed by a fix_value component
        var fv = GetComponent<fix_value>();
        if (fv != null) return fv.value;

        int max = 1; // If this isn't made in a recipe, default to a value of 1
        foreach (var kv in recipe.all_recipies())
        {
            var rs = kv.Value;
            foreach (var r in rs)
            {
                float amt_produced = r.average_amount_produced(this);
                if (amt_produced > 0)
                {
                    float derived_value = r.average_ingredients_value() / amt_produced;
                    derived_value = (derived_value + 1) * 1.2f;
                    if (derived_value > max)
                    {
                        max = Mathf.CeilToInt(derived_value);
                        recipe_determining_value = r;
                    }
                }
            }
        }
        return max;
    }

    public class prefab_editor : System.IDisposable
    {
        public readonly string path;
        public readonly GameObject prefab;

        public prefab_editor(GameObject prefabRoot)
        {
            this.prefab = prefabRoot;
            this.path = UnityEditor.AssetDatabase.GetAssetPath(prefabRoot);
            this.prefab = UnityEditor.PrefabUtility.LoadPrefabContents(path);
        }

        public void Dispose()
        {
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(prefab, path);
            UnityEditor.PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    static void solve_item_values()
    {
        var ivs = new GameObject("value_solver").AddComponent<item_value_solver>();
    }

    [ExecuteInEditMode]
    class item_value_solver : MonoBehaviour
    {
        item[] all_items;
        int iteration = 0;
        private void Start()
        {
            all_items = Resources.LoadAll<item>("items");

            // Initial condition is all items have value 1
            foreach (var i in all_items)
                i.value = 1;
        }

        private void Update()
        {
            ++iteration;
            int changed = 0;
            foreach (var i in all_items)
            {
                int suggested = i.suggested_value(out recipe r);
                if (suggested != i.value)
                {
                    i.value = suggested;
                    ++changed;
                }
            }

            Debug.Log("Value solver iteration " + iteration + " values changed: " + changed);
            if (changed == 0)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
        }

        private void OnDestroy()
        {
            var lst = new List<item>(all_items);
            lst.Sort((a, b) => b.value.CompareTo(a.value));
            string summary = "Resulting item values\n";
            foreach (var i in lst)
                summary += i.name + " " + i.value + "\n";
            Debug.Log(summary);

            // Write to prefabs
            foreach (var i in all_items)
                using (var pe = new prefab_editor(i.gameObject))
                    pe.prefab.GetComponent<item>().value = i.value;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(item), true)]
    class item_editor : UnityEditor.Editor
    {
        recipe rec;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            item i = (item)target;
            if (UnityEditor.EditorGUILayout.Toggle("Suggest value", false))
            {
                i.value = i.suggested_value(out recipe r);
                rec = r;
            }
            if (UnityEditor.EditorGUILayout.Toggle("Solve all values", false))
                solve_item_values();

            if (rec != null)
                UnityEditor.EditorGUILayout.ObjectField("Recipe determining value: ", rec, typeof(recipe), false);
        }
    }
#endif
}