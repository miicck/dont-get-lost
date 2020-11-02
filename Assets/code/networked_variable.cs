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
    public void reccive_serialization(byte[] buffer, ref int offset, int length)
    {
        process_serialization(buffer, ref offset, length);
    }

    /// <summary> Serialization bytes queued for sending to the server. </summary>
    public byte[] queued_serial;

    /// <summary> Serialize my value into a form suitable
    /// for sending over the network </summary>
    public abstract byte[] serialization();

    /// <summary> Process a serialization recived from the network. </summary>
    protected abstract void process_serialization(byte[] buffer, ref int offset, int length);

    /// <summary> Call to let the networking engine know the serialization has changed. </summary>
    public void set_dirty() { queued_serial = serialization(); }
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
            on_change_old_new?.Invoke(old_value, value);
            initialized = true;
        }
    }
    protected T _value;

    /// <summary> The last value that was sent. </summary>
    T last_queued_value;

    /// <summary> Checks <paramref name="new_value"/> for validity, 
    /// should return the nearest valid T. By default, just returns
    /// <paramref name="new_value"/>. </summary>
    protected virtual T validate(T new_value) { return new_value; }

    /// <summary> Function called every time the variable changes value. </summary>
    public on_change_func on_change;
    public delegate void on_change_func();

    /// <summary> Function called every time the variable changes value, 
    /// but with old+new values provided </summary>
    public on_change_old_new_func on_change_old_new;
    public delegate void on_change_old_new_func(T old_value, T new_value);

    /// <summary> True if value has been initialized, either from
    /// the server via <see cref="networked_variable.reccive_serialization(byte[], int, int)"/>,
    /// or by a client setting <see cref="value"/>. </summary>
    public bool initialized { get; private set; }

    /// <summary> Called when a serialization of this variable is reccived </summary>
    protected override void process_serialization(byte[] buffer, ref int offset, int length)
    {
        // Set the value directly to avoid sending another update
        T old_value = _value;
        _value = deserialize(buffer, ref offset, length);
        if (!initialized || !_value.Equals(old_value))
        {
            on_change?.Invoke();
            on_change_old_new?.Invoke(old_value, _value);
        }
        initialized = true;
    }

    /// <summary> Recover a value from its serialization. </summary>
    protected abstract T deserialize(byte[] buffer, ref int offset, int length);

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
}

public class networked_list<T> : networked_variable, IEnumerable<T>
    where T : networked_variable, new()
{
    List<T> list = new List<T>();
    public IEnumerator<T> GetEnumerator() { return list.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

    public void add(T t)
    {
        list.Add(t);
        set_dirty();
    }

    public bool remove(T t)
    {
        if (list.Remove(t))
        {
            set_dirty();
            return true;
        }
        return false;
    }

    public void remove_at(int i)
    {
        list.RemoveAt(i);
        set_dirty();
    }

    public T this[int i] => list[i];
    public int length => list.Count;

    protected override void process_serialization(byte[] buffer, ref int offset, int length)
    {
        int start = offset;
        int list_length = network_utils.decode_int(buffer, ref offset);
        list = new List<T>();

        for (int i = 0; i < list_length; ++i)
        {
            var t = new T();
            t.reccive_serialization(buffer, ref offset, length - (offset - start));
            list.Add(t);
        }
    }

    public override byte[] serialization()
    {
        List<byte> serial = new List<byte>();
        serial.AddRange(network_utils.encode_int(list.Count));
        foreach (var t in list) serial.AddRange(t.serialization());
        return serial.ToArray();
    }
}

public class networked_pair<T, K> : networked_variable
    where T : networked_variable, new() where K : networked_variable, new()
{
    KeyValuePair<T, K> pair;
    public T first => pair.Key;
    public K second => pair.Value;

    public networked_pair(T first, K second)
    {
        pair = new KeyValuePair<T, K>(first, second);
    }

    public networked_pair()
    {
        pair = new KeyValuePair<T, K>(new T(), new K());
    }

    protected override void process_serialization(byte[] buffer, ref int offset, int length)
    {
        int start = offset;
        pair.Key.reccive_serialization(buffer, ref offset, length);
        pair.Value.reccive_serialization(buffer, ref offset, length - (offset - start));
    }

    public override byte[] serialization()
    {
        return network_utils.concat_buffers(
            pair.Key.serialization(), pair.Value.serialization());
    }
}

public class networked_int_set : networked_variable, IEnumerable<int>
{
    HashSet<int> set = new HashSet<int>();
    public IEnumerator<int> GetEnumerator() { return set.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

    public void add(int i)
    {
        if (!set.Add(i)) return; // Already in the set
        set_dirty();
    }

    public void remove(int i)
    {
        if (!set.Remove(i)) return; // Wasn't in the set
        set_dirty();
    }

    public bool contains(int i) => set.Contains(i);

    public override byte[] serialization()
    {
        List<byte> serial = new List<byte>(sizeof(int) * (set.Count + 1));
        serial.AddRange(network_utils.encode_int(set.Count));
        foreach (var i in set)
            serial.AddRange(network_utils.encode_int(i));
        return serial.ToArray();
    }

    protected override void process_serialization(byte[] buffer, ref int offset, int length)
    {
        int start = offset;
        int count = network_utils.decode_int(buffer, ref offset);
        set = new HashSet<int>();
        for (int i = 0; i < count; ++i)
            set.Add(network_utils.decode_int(buffer, ref offset));
        if (offset - start != length)
            throw new System.Exception("int set not correctly deserialized!");
    }
}

//#################//
// IMPLEMENTATIONS //
//#################//

namespace networked_variables
{
    /// <summary> A networked boolean value. </summary>
    public class net_bool : networked_variable<bool>
    {
        public net_bool() { }
        public net_bool(bool default_value = false)
        {
            _value = default_value;
        }

        public override byte[] serialization()
        {
            return network_utils.encode_bool(value);
        }

        protected override bool deserialize(byte[] buffer, ref int offset, int length)
        {
            return network_utils.decode_bool(buffer, ref offset);
        }
    }

    /// <summary> A simple networked integer. </summary>
    public class net_int : networked_variable<int>
    {
        int min_value;
        int max_value;

        public net_int(int default_value = 0,
            int min_value = int.MinValue,
            int max_value = int.MaxValue) : base()
        {
            this.min_value = min_value;
            this.max_value = max_value;
            _value = validate(default_value);
        }

        protected override int validate(int new_value)
        {
            if (new_value < min_value) return min_value;
            if (new_value > max_value) return max_value;
            return new_value;
        }

        public override byte[] serialization()
        {
            return network_utils.encode_int(value);
        }

        protected override int deserialize(byte[] buffer, ref int offset, int length)
        {
            return network_utils.decode_int(buffer, ref offset);
        }
    }

    /// <summary> A networked string. </summary>
    public class net_string : networked_variable<string>
    {
        public net_string(string default_value)
        {
            _value = default_value;
        }

        public net_string()
        {
            _value = "";
        }

        public override byte[] serialization()
        {
            return network_utils.encode_string(value);
        }

        protected override string deserialize(byte[] buffer, ref int offset, int length)
        {
            return network_utils.decode_string(buffer, ref offset);
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

        protected override float deserialize(byte[] buffer, ref int offset, int length)
        {
            return network_utils.decode_float(buffer, ref offset);
        }

        protected override bool should_send(float last_sent, float new_value)
        {
            // Only send values that have changed by more than the resolution
            return Mathf.Abs(last_sent - new_value) > resolution;
        }
    }

    /// <summary> A networked Vector3 </summary>
    public class net_vector3 : networked_variable<Vector3>
    {
        float lerp_speed = 5f;

        public Vector3 lerped_value
        {
            get
            {
                _lerped_value = Vector3.Lerp(_lerped_value, value, Time.deltaTime * lerp_speed);
                return _lerped_value;
            }
        }
        Vector3 _lerped_value;

        public void reset_lerp()
        {
            _lerped_value = value;
        }

        public net_vector3() { }

        public net_vector3(float lerp_speed = 5f, Vector3 default_value = default)
        {
            this.lerp_speed = lerp_speed;
            _value = default_value;
            _lerped_value = default_value;
        }

        public override byte[] serialization()
        {
            return network_utils.concat_buffers(
                network_utils.encode_float(value.x),
                network_utils.encode_float(value.y),
                network_utils.encode_float(value.z)
            );
        }

        protected override Vector3 deserialize(byte[] buffer, ref int offset, int length)
        {
            return new Vector3(
                network_utils.decode_float(buffer, ref offset),
                network_utils.decode_float(buffer, ref offset),
                network_utils.decode_float(buffer, ref offset)
            );
        }
    }

    /// <summary> A networked color. </summary>
    public class net_color : networked_variable<Color>
    {
        public net_color() { }
        public net_color(Color default_value) { _value = default_value; }

        public override byte[] serialization()
        {
            return network_utils.concat_buffers(
                network_utils.encode_float(value.r),
                network_utils.encode_float(value.g),
                network_utils.encode_float(value.b),
                network_utils.encode_float(value.a)
            );
        }

        protected override Color deserialize(byte[] buffer, ref int offset, int length)
        {
            return new Color(
                network_utils.decode_float(buffer, ref offset),
                network_utils.decode_float(buffer, ref offset),
                network_utils.decode_float(buffer, ref offset),
                network_utils.decode_float(buffer, ref offset)
            );
        }
    }

    /// <summary> A networked rotation. </summary>
    public class net_quaternion : networked_variable<Quaternion>
    {
        float lerp_speed = 5f;

        public Quaternion lerped_value
        {
            get
            {
                _lerped_value = Quaternion.Lerp(_lerped_value, value, Time.deltaTime * lerp_speed);
                return _lerped_value;
            }
        }
        Quaternion _lerped_value = Quaternion.identity;

        public net_quaternion() { }

        public net_quaternion(float lerp_speed = 5f, Quaternion default_value = default)
        {
            this.lerp_speed = lerp_speed;
            if (default_value.Equals(default))
                default_value = Quaternion.identity;

            _lerped_value = default_value;
            _value = default_value;
        }

        public override byte[] serialization()
        {
            return network_utils.concat_buffers(
                network_utils.encode_float(value.x),
                network_utils.encode_float(value.y),
                network_utils.encode_float(value.z),
                network_utils.encode_float(value.w)
            );
        }

        protected override Quaternion deserialize(byte[] buffer, ref int offset, int length)
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
        public net_string_counts()
        {
            _value = new SortedDictionary<string, int>();
        }

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

        protected override SortedDictionary<string, int> deserialize(byte[] buffer, ref int offset, int length)
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
    }

    public class net_string_counts_v2 : networked_variable, IEnumerable<KeyValuePair<string, int>>
    {
        Dictionary<string, int> dict = new Dictionary<string, int>();
        public IEnumerator<KeyValuePair<string, int>> GetEnumerator() { return dict.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        public int count => dict.Count;

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

        public void set(Dictionary<string, int> new_value)
        {
            if (utils.compare_dictionaries(dict, new_value))
                return; // No change

            // Set the entire dictionary in one operation
            // (to avoid calling on_change/serialize for every key)
            dict.Clear();
            foreach (var kv in new_value)
                dict[kv.Key] = kv.Value;

            queued_serial = serialization();
            on_change?.Invoke();
        }

        public void clear()
        {
            // Clear the dictionary in one operation
            // (to avoid calling on_change/serialize for every key)
            dict.Clear();

            queued_serial = serialization();
            on_change?.Invoke();
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

        protected override void process_serialization(byte[] buffer, ref int offset, int length)
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