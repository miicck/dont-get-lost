using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface INonEquipable { }
public interface INonEquipableCallback : INonEquipable { void on_equip_callback(player player); }
public interface INonLogistical { }

public class item : networked, IPlayerInteractable
{
    public const float LOGISTICS_SIZE = 0.3f;

    //###########//
    // VARIABLES //
    //###########//

    public Sprite sprite; // The sprite represeting this item in inventories etc
    public string plural;
    public int value;
    public bool is_equpped => GetComponentInParent<player>() != null;

    public food food_values => this?.GetComponent<food>();

    public int fuel_value
    {
        get
        {
            var fv = GetComponent<fuel_value>();
            return fv == null ? 0 : fv.fuel_value_amount;
        }
    }

    void make_logistics_version()
    {
        if (!is_client_side)
            throw new System.Exception("Can only make client side items into the logstics version!");

        is_logistics_version = true;
        transform.localScale *= logistics_scale;

        // Remove components that are incompatible with the logistics version
        foreach (var c in GetComponentsInChildren<Component>())
            if (c is INonLogistical) Destroy(c);
    }

    public bool is_logistics_version { get; private set; }

    // How much to scale the item by when it is in the logistics network
    public float logistics_scale
    {
        get
        {
            if (_logistics_scale < 0)
                _logistics_scale = suggested_logistics_scale();
            return _logistics_scale;
        }
    }
    float _logistics_scale = -1f;

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

    public virtual player_interaction[] player_interactions(RaycastHit hit)
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

        public override bool is_possible()
        {
            return item != null && !item.is_logistics_version;
        }

        protected override bool on_start_interaction(player player)
        {
            player.play_sound("sounds/click_1", volume: 0.5f);
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

        protected override bool on_start_interaction(player player)
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

    // No default interactions
    player_interaction[] interactions = new player_interaction[0];
    public virtual player_interaction[] item_uses() => interactions;

    abstract class player_item_interaction : player_interaction
    {
        protected item item { get; private set; }
        public player_item_interaction(item item) { this.item = item; }
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

    protected delegate void recover_settings_func(building_material copy);
    protected virtual recover_settings_func get_recover_func() => null;

    public undo_manager.undo_action pickup_undo()
    {
        if (this == null) return null; // Destroyed

        // Copies for lambda
        var pickup_items = add_to_inventory_on_pickup();
        string name_copy = string.Copy(name);
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;
        networked parent = transform.parent?.GetComponent<networked>();
        var rf = get_recover_func();

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
            var created = create(name_copy, pos, rot, networked: true, parent) as building_material;
            rf?.Invoke(created);

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
        string info = (quantity < 2 ? item.display_name : (utils.int_to_comma_string(quantity) + " " + item.plural)) + "\n";

        // Value
        if (quantity > 1)
            info += "Value: " + (item.value * quantity).qs() + " (" + item.value.qs() + " each)\n";
        else
            info += "Value: " + item.value.qs() + "\n";

        // Can this item be built with
        if (item is building_material)
            info += "  Can be used for building\n";

        // Fuel value
        if (item.fuel_value > 0)
        {
            if (quantity > 1)
                info += "Fuel value: " + (item.fuel_value * quantity).qs() + " (" + item.fuel_value.qs() + " each)\n";
            else
                info += "Fuel value: " + item.fuel_value.qs() + "\n";
        }

        // Food value
        if (item.food_values != null)
        {
            info += "Food values: " + item.food_values.shorthand_notation() + "\n";
            var me = item.GetComponent<food_mood_effect>();
            if (me != null) info += "Food mood effect " + (me.effect.delta_mood > 0 ? "+" : "") + me.effect.delta_mood;
        }

        if (item is contract)
        {
            var c = (contract)item;
            info += c.info(player.current);
        }

        return info.Trim();
    }

    int suggested_value(out IRecipeInfo recipe_determining_value)
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

    float suggested_logistics_scale()
    {
        var b = visual_bounds();
        float max_size = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (max_size < 10e-4) return 1f; // Avoid divide-by-zero
        return Mathf.Min(1f, LOGISTICS_SIZE / max_size); // Don't allow scaling up
    }

    private void OnDrawGizmosSelected()
    {
        var b = visual_bounds();
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(b.center, b.size);
    }

    //########//
    // BOUNDS //
    //########//

    public Bounds visual_bounds() => this.bounds_by_type<MeshRenderer>((r) => r.bounds);
    public Bounds collision_bounds() => this.bounds_by_type<Collider>((c) => c.bounds);

    //##################//
    // EDITOR UTILITIES //
    //##################//
#if UNITY_EDITOR
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
                int suggested = i.suggested_value(out IRecipeInfo r);
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
                using (var pe = new utils.prefab_editor(i.gameObject))
                    pe.prefab.GetComponent<item>().value = i.value;
        }
    }

    [UnityEditor.CustomEditor(typeof(item), true)]
    class item_editor : UnityEditor.Editor
    {
        IRecipeInfo rec;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            item i = (item)target;
            if (GUILayout.Button("Suggest value"))
            {
                i.value = i.suggested_value(out IRecipeInfo r);
                rec = r;
            }
            if (GUILayout.Button("Solve all values"))
                solve_item_values();

            if (rec != null)
                UnityEditor.EditorGUILayout.TextField("Recipe determining value: " + rec.recipe_book_string());
        }
    }
#endif
}