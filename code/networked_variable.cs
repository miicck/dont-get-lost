using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Tag a networked variable with a predefined type, because 
/// it has a special meaning to the network engine. </summary>
public class engine_networked_variable : System.Attribute
{
    public enum TYPE : int
    {
        POSITION_X,
        POSITION_Y,
        POSITION_Z,
    }

    public TYPE type;
    public engine_networked_variable(TYPE type) { this.type = type; }
}

public abstract class networked_variable
{
    /// <summary> Reccive a serialization from the server. </summary>
    public void reccive_serialization(byte[] buffer, int offset, int length)
    {
        process_serialization(buffer, offset, length);
    }

    /// <summary> Serialization bytes queued for sending to the server. </summary>
    public byte[] queued_serial;

    /// <summary> Serialize my value into a form suitable
    /// for sending over the network </summary>
    public abstract byte[] serialization();

    protected abstract void process_serialization(byte[] buffer, int offset, int length);
}

/// <summary> A value serialized over the network. </summary>
public abstract class networked_variable<T> : networked_variable
{
    public T value
    {
        get => _value;
        set
        {
            // Before we do anything, validate the value
            value = validate(value);

            if (_value == default)
            {
                if (value == default)
                    return; // No change, still default
            }
            else if (_value.Equals(value))
                return; // No change

            T old_value = _value;
            _value = value;

            if (should_send(last_queued_value, _value))
            {
                queued_serial = serialization();
                last_queued_value = value;
            }

            on_change?.Invoke();
            initialized = true;
        }
    }
    T _value;

    /// <summary> The last value that was sent. </summary>
    T last_queued_value;

    /// <summary> Constructor. </summary>
    public networked_variable()
    {
        _value = default_value();
    }

    /// <summary> Checks <paramref name="new_value"/> for validity, 
    /// should return the nearest valid T. By default, just returns
    /// <paramref name="new_value"/>. </summary>
    protected virtual T validate(T new_value) { return new_value; }

    /// <summary> Function called every time the variable changes value. </summary>
    public on_change_func on_change;
    public delegate void on_change_func();

    /// <summary> True if value has been initialized, either from
    /// the server via <see cref="networked_variable.reccive_serialization(byte[], int, int)"/>,
    /// or by a client setting <see cref="value"/>. </summary>
    public bool initialized { get; private set; }

    /// <summary> Called when a serialization of this variable is reccived </summary>
    protected override void process_serialization(byte[] buffer, int offset, int length)
    {
        // Set the value directly to avoid sending another update
        T old_value = _value;
        _value = deserialize(buffer, offset, length);
        on_change?.Invoke();
        initialized = true;
    }

    /// <summary> Recover a value from its serialization. </summary>
    protected abstract T deserialize(byte[] buffer, int offset, int length);

    /// <summary> Returns true if the new value is different 
    /// enough from the last sent value to warrant sending. 
    /// This is useful for reducing network traffic by only
    /// sending sufficiently large changes. </summary>
    protected virtual bool should_send(T last_sent, T new_value)
    {
        if (last_sent == default)
            return new_value != default;

        return !last_sent.Equals(new_value);
    }

    /// <summary> The default value that the variable should take. </summary>
    protected virtual T default_value() { return default; }
}

//#################//
// IMPLEMENTATIONS //
//#################//

namespace networked_variables
{
    /// <summary> A simple networked integer. </summary>
    public class net_int : networked_variable<int>
    {
        public override byte[] serialization()
        {
            return network_utils.encode_int(value);
        }

        protected override int deserialize(byte[] buffer, int offset, int length)
        {
            return network_utils.decode_int(buffer, ref offset);
        }
    }

    /// <summary> A networked string. </summary>
    public class net_string : networked_variable<string>
    {
        public override byte[] serialization()
        {
            return network_utils.encode_string(value);
        }

        protected override string deserialize(byte[] buffer, int offset, int length)
        {
            return network_utils.decode_string(buffer, ref offset);
        }

        protected override string default_value()
        {
            return ""; // Can't serialize null strings
        }
    }

    /// <summary> A networked floating point number, supporting resolution + lerp. </summary>
    public class net_float : networked_variable<float>
    {
        /// <summary> A smoothed value. </summary>
        public float lerped_value
        {
            get
            {
                _lerp_value = Mathf.Lerp(_lerp_value, value, Time.deltaTime * lerp_speed);
                return _lerp_value;
            }
        }
        float _lerp_value;

        /// <summary> Sets the lerped value = networked value 
        /// (i.e fast-forwards the lerping to completion) </summary>
        public void reset_lerp()
        {
            _lerp_value = value;
        }

        float lerp_speed;
        float resolution;
        float max_value;
        float min_value;

        public net_float(float lerp_speed = 5f, float resolution = 0f,
            float max_value = float.PositiveInfinity,
            float min_value = float.NegativeInfinity)
        {
            this.lerp_speed = lerp_speed;
            this.resolution = resolution;
            this.min_value = min_value;
            this.max_value = max_value;
        }

        protected override float validate(float new_value)
        {
            if (new_value > max_value) return max_value;
            if (new_value < min_value) return min_value;
            return new_value;
        }

        public override byte[] serialization()
        {
            return network_utils.encode_float(value);
        }

        protected override float deserialize(byte[] buffer, int offset, int length)
        {
            return network_utils.decode_float(buffer, ref offset);
        }

        protected override bool should_send(float last_sent, float new_value)
        {
            // Only send values that have changed by more than the resolution
            return Mathf.Abs(last_sent - new_value) > resolution;
        }
    }

    /// <summary> A networked rotation. </summary>
    public class net_quaternion : networked_variable<Quaternion>
    {
        public override byte[] serialization()
        {
            return network_utils.concat_buffers(
                network_utils.encode_float(value.x),
                network_utils.encode_float(value.y),
                network_utils.encode_float(value.z),
                network_utils.encode_float(value.w)
            );
        }

        protected override Quaternion deserialize(byte[] buffer, int offset, int length)
        {
            return new Quaternion(
                network_utils.decode_float(buffer, ref offset),
                network_utils.decode_float(buffer, ref offset),
                network_utils.decode_float(buffer, ref offset),
                network_utils.decode_float(buffer, ref offset)
            );
        }
    }

    /// <summary> Represents a map from strings to ints. </summary>
    public class net_string_counts : networked_variable<SortedDictionary<string, int>>
    {
        public override byte[] serialization()
        {
            List<byte> ret = new List<byte>();
            foreach (var kv in value)
            {
                ret.AddRange(network_utils.encode_string(kv.Key));
                ret.AddRange(network_utils.encode_int(kv.Value));
            }
            return ret.ToArray();
        }

        protected override SortedDictionary<string, int> deserialize(byte[] buffer, int offset, int length)
        {
            var new_dict = new SortedDictionary<string, int>();

            int end = offset + length;
            while (offset < end)
            {
                string key = network_utils.decode_string(buffer, ref offset);
                int value = network_utils.decode_int(buffer, ref offset);
                new_dict[key] = value;
            }

            return new_dict;
        }

        protected override SortedDictionary<string, int> default_value()
        {
            return new SortedDictionary<string, int>();
        }
    }

    public class net_inventory : networked_variable<Dictionary<int, KeyValuePair<string, int>>>
    {
        public override byte[] serialization()
        {
            List<byte> serial = new List<byte>();

            foreach (var slot in value)
            {
                var slot_number = slot.Key;
                var name = slot.Value.Key;
                var count = slot.Value.Value;
                serial.AddRange(network_utils.encode_int(slot_number));
                serial.AddRange(network_utils.encode_string(name));
                serial.AddRange(network_utils.encode_int(count));
            }

            return serial.ToArray();
        }

        protected override Dictionary<int, KeyValuePair<string, int>> deserialize(
            byte[] buffer, int offset, int length)
        {
            Dictionary<int, KeyValuePair<string, int>> ret =
                new Dictionary<int, KeyValuePair<string, int>>();

            int end = offset + length;
            while (offset < end)
            {
                var slot_number = network_utils.decode_int(buffer, ref offset);
                var name = network_utils.decode_string(buffer, ref offset);
                var count = network_utils.decode_int(buffer, ref offset);
                ret[slot_number] = new KeyValuePair<string, int>(name, count);
            }

            return ret;
        }

        protected override Dictionary<int, KeyValuePair<string, int>> default_value()
        {
            return new Dictionary<int, KeyValuePair<string, int>>();
        }

        bool updating_from_network;

        public static net_inventory new_linked_to(inventory_section section)
        {
            var net_inventory = new net_inventory();

            section.add_on_change_listener(() =>
            {
                if (net_inventory.updating_from_network) return;

                // Keep the network up up to date with changes
                var new_values = new Dictionary<int, KeyValuePair<string, int>>();
                for (int i = 0; i < section.slots.Length; ++i)
                    if (section.slots[i].item != null)
                        new_values[i] = new KeyValuePair<string, int>(
                            section.slots[i].item, section.slots[i].count);

                net_inventory.value = new_values;
            });

            net_inventory.on_change = () =>
            {
                net_inventory.updating_from_network = true;

                // Update the slot contents from the network
                for (int i = 0; i < section.slots.Length; ++i)
                {
                    if (net_inventory.value.TryGetValue(i, out KeyValuePair<string, int> name_contents))
                        section.slots[i].set_item_count(name_contents.Key, name_contents.Value);
                    else
                        section.slots[i].clear();
                }

                net_inventory.updating_from_network = false;
            };

            return net_inventory;
        }
    }

    public class net_string_counts_v2 : networked_variable, IEnumerable<KeyValuePair<string, int>>
    {
        Dictionary<string, int> dict = new Dictionary<string, int>();
        public IEnumerator<KeyValuePair<string, int>> GetEnumerator() { return dict.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        public int this[string s]
        {
            get
            {
                // A key not present => 0
                if (!dict.TryGetValue(s, out int count)) return 0;
                return count;
            }

            set
            {
                // Check if we've actually modified the dictionary
                if (dict.TryGetValue(s, out int count))
                {
                    if (count == value)
                        return; // No change, count is the same
                }
                else if (value == 0)
                    return; // No change, still not present

                // Modify the dictionary + schedue network update
                if (value == 0) dict.Remove(s);
                else dict[s] = value;
                queued_serial = serialization();
                on_change?.Invoke();
            }
        }

        public override byte[] serialization()
        {
            // Serialize the dictionary
            List<byte> serial = new List<byte>();
            foreach (var kv in dict)
            {
                serial.AddRange(network_utils.encode_string(kv.Key));
                serial.AddRange(network_utils.encode_int(kv.Value));
            }
            return serial.ToArray();
        }

        protected override void process_serialization(byte[] buffer, int offset, int length)
        {
            // Deserialize a dictionary
            var new_dict = new Dictionary<string, int>();
            int end = offset + length;
            while (offset < end)
            {
                string key = network_utils.decode_string(buffer, ref offset);
                int value = network_utils.decode_int(buffer, ref offset);
                new_dict[key] = value;
            }

            // Check if the dictionary has changed
            if (!utils.compare_dictionaries(new_dict, dict))
            {
                dict = new_dict;
                on_change?.Invoke();
            }
        }

        public delegate void on_change_func();
        public on_change_func on_change;
    }
}