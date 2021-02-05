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
    public void set_owner_and_index(networked owner, int index)
    {
        this.owner = owner;
        this.index = index;
    }

    networked owner;
    public int index { get; private set; }
    public int network_id => owner == null ? -1 : owner.network_id;

    /// <summary> Reccive a serialization from the server. </summary>
    public void reccive_serialization(byte[] buffer, ref int offset, int length)
    {
        process_serialization(buffer, ref offset, length);
    }

    /// <summary> Serialize my value into a form suitable
    /// for sending over the network </summary>
    public abstract byte[] serialization();

    /// <summary> Process a serialization recived from the network. </summary>
    protected abstract void process_serialization(byte[] buffer, ref int offset, int length);

    /// <summary> Call to let the networking engine know the serialization has changed. </summary>
    public virtual void set_dirty()
    {
        client.queue_variable_update(this);
    }
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

            if (_value.Equals(default))
            {
                if (value.Equals(default))
                    return; // No change, still default
            }
            else if (_value.Equals(value))
                return; // No change

            T old_value = _value;
            _value = value;

            if (should_send(last_sent_value, _value))
            {
                set_dirty();
                last_sent_value = value;
            }

            on_change?.Invoke();
            on_change_old_new?.Invoke(old_value, value);
            initialized = true;
        }
    }
    protected T _value;

    /// <summary> The hash code is inherited from 
    /// the underlying type <typeparamref name="T"/>. </summary>
    public override int GetHashCode() { return value.GetHashCode(); }

    /// <summary> Equality is inherited from the underlying 
    /// type <typeparamref name="T"/>. </summary>
    public override bool Equals(object obj)
    {
        if (obj is networked_variable<T>)
        {
            var other = (networked_variable<T>)obj;
            return value.Equals(other.value);
        }
        return false;
    }

    /// <summary> The last value that was sent. </summary>
    T last_sent_value;

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

        try
        {
            _value = deserialize(buffer, ref offset, length);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to deserialize networked variable: " + e.Message);
            return;
        }

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
        if (last_sent == null) return new_value != null;
        if (new_value == null) return last_sent != null;
        if (last_sent.Equals(default)) return !new_value.Equals(default);

        return !last_sent.Equals(new_value);
    }
}

//#################//
// IMPLEMENTATIONS //
//#################//

namespace networked_variables
{
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

        public void clear()
        {
            list.Clear();
            set_dirty();
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
        where T : networked_variable, new()
        where K : networked_variable, new()
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

    public class simple_networked_pair<T, K> : networked_variable
    {
        public T first
        {
            get => net_first.value;
            set
            {
                net_first.value = value;
                on_change?.Invoke();
                set_dirty();
            }
        }

        public K second
        {
            get => net_second.value;
            set
            {
                net_second.value = value;
                on_change?.Invoke();
                set_dirty();
            }
        }

        public void set(T first, K second)
        {
            net_first.value = first;
            net_second.value = second;
            on_change?.Invoke();
            set_dirty();
        }

        networked_variable<T> net_first;
        networked_variable<K> net_second;

        public simple_networked_pair(
            networked_variable<T> first,
            networked_variable<K> second)
        {
            net_first = first;
            net_second = second;
        }

        protected override void process_serialization(byte[] buffer, ref int offset, int length)
        {
            int start = offset;
            net_first.reccive_serialization(buffer, ref offset, length);
            net_second.reccive_serialization(buffer, ref offset, length - (offset - start));
            on_change?.Invoke();
        }

        public override byte[] serialization()
        {
            return network_utils.concat_buffers(
                net_first.serialization(),
                net_second.serialization());
        }

        public delegate void on_change_func();
        public on_change_func on_change;
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

        public net_int() : base()
        {
            this.min_value = int.MinValue;
            this.max_value = int.MaxValue;
            _value = 0;
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

    public class net_string_counts : networked_variable, IEnumerable<KeyValuePair<string, int>>
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

                set_dirty();
                on_change?.Invoke();
            }
        }

        public bool contains_key(string s) => dict.ContainsKey(s);

        public void set(Dictionary<string, int> new_value)
        {
            if (utils.compare_dictionaries(dict, new_value))
                return; // No change

            // Set the entire dictionary in one operation
            // (to avoid calling on_change/serialize for every key)
            dict.Clear();
            foreach (var kv in new_value)
                dict[kv.Key] = kv.Value;

            set_dirty();
            on_change?.Invoke();
        }

        public void clear()
        {
            // Clear the dictionary in one operation
            // (to avoid calling on_change/serialize for every key)
            dict.Clear();

            set_dirty();
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

    public class net_dictionary<K, V> : networked_variable, IEnumerable<KeyValuePair<K, V>>
        where K : networked_variable, new()
        where V : networked_variable, new()
    {
        Dictionary<K, V> dict = new Dictionary<K, V>();
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() { return dict.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        public bool TryGetValue(K key, out V val) { return dict.TryGetValue(key, out val); }
        public V this[K key]
        {
            get
            {
                if (dict.ContainsKey(key))
                    return dict[key];
                return null;
            }
            set
            {
                dict[key] = value;
                set_dirty();
            }
        }

        public override byte[] serialization()
        {
            List<byte> bytes = new List<byte>();
            foreach (var kv in dict)
            {
                var key_serial = kv.Key.serialization();
                bytes.AddRange(network_utils.encode_int(key_serial.Length));
                bytes.AddRange(key_serial);

                var val_serial = kv.Value.serialization();
                bytes.AddRange(network_utils.encode_int(val_serial.Length));
                bytes.AddRange(val_serial);
            }
            return bytes.ToArray();
        }

        protected override void process_serialization(byte[] buffer, ref int offset, int length)
        {
            int end = offset + length;
            while (offset < end)
            {
                K key = new K();
                int key_length = network_utils.decode_int(buffer, ref offset);
                key.reccive_serialization(buffer, ref offset, key_length);

                V val = new V();
                int val_length = network_utils.decode_int(buffer, ref offset);
                val.reccive_serialization(buffer, ref offset, val_length);

                dict[key] = val;
            }
        }
    }

    public class net_food_satisfaction : networked_variable
    {
        byte[] group_values = new byte[food.total_groups];

        public static net_food_satisfaction fully_satisfied
        {
            get
            {
                var ret = new net_food_satisfaction();
                for (int i = 0; i < ret.group_values.Length; ++i)
                    ret.group_values[i] = byte.MaxValue;
                return ret;
            }
        }

        public byte metabolic_satisfaction
        {
            get; private set;
        }

        void eval_metabolic_sat()
        {
            byte max = 0;
            foreach (var g in food.all_groups)
                if (food.can_metabolize(g))
                    if (this[g] > max)
                        max = this[g];
            metabolic_satisfaction = max;
        }

        public byte hunger => (byte)(byte.MaxValue - metabolic_satisfaction);

        public byte this[food.GROUP group]
        {
            get { return group_values[(int)group]; }
            set
            {
                if (value == group_values[(int)group])
                    return; // No change

                group_values[(int)group] = value;
                eval_metabolic_sat();
                set_dirty();
            }
        }

        public void consume_food(food f)
        {
            bool dirty = false;
            foreach (var g in food.all_groups)
            {
                byte val_before = group_values[(int)g];
                group_values[(int)g] = (byte)Mathf.Min(byte.MaxValue, val_before + f.food_value(g));
                if (group_values[(int)g] != val_before)
                    dirty = true;
            }
            if (dirty)
            {
                eval_metabolic_sat();
                set_dirty();
            }
        }

        public void modify_every_satisfaction(int amount)
        {
            for (int i = 0; i < group_values.Length; ++i)
                group_values[i] = (byte)Mathf.Clamp(group_values[i] + amount, 0, byte.MaxValue);

            eval_metabolic_sat();
            set_dirty();
        }

        public override byte[] serialization()
        {
            return group_values;
        }

        protected override void process_serialization(byte[] buffer, ref int offset, int length)
        {
            if (length != food.total_groups)
                throw new System.Exception("Food satisfaction serialization has incorrect length!");
            System.Buffer.BlockCopy(buffer, offset, group_values, 0, length);
            eval_metabolic_sat();
            offset += length;
        }
    }

    public class net_job_enable_state : networked_variable
    {
        // 0 if the job is enabled, 1 if not 
        // (just so the default is all-enabled)
        byte[] enabled = new byte[job_type.all.Length];

        public bool this[job_type j]
        {
            get => enabled[j.default_priority] == 0;
            set
            {
                byte new_val = (byte)(value ? 0 : 1);
                if (new_val == enabled[j.default_priority]) return; // No change
                enabled[j.default_priority] = new_val;
                set_dirty();
            }
        }

        public override byte[] serialization()
        {
            return enabled;
        }

        protected override void process_serialization(byte[] buffer, ref int offset, int length)
        {
            if (length != enabled.Length)
                throw new System.Exception("Incorrect serialization length!");
            System.Buffer.BlockCopy(buffer, offset, enabled, 0, length);
        }
    }

    public class net_job_priorities : networked_variable
    {
        /// <summary> The position in the priority list of each job type,
        /// indexed by the default priority of the job. </summary>
        byte[] priorities = new byte[job_type.all.Length];

        /// <summary> The job types ordered according to <see cref="priorities"/>. </summary>
        public job_type[] ordered
        {
            get
            {
                if (_ordered == null) work_out_order();
                return _ordered;
            }
        }
        job_type[] _ordered;

        public int priority(job_type j)
        {
            return priorities[j.default_priority];
        }

        void work_out_order()
        {
            // Work out the job type order
            _ordered = new job_type[job_type.all.Length];
            foreach (var j in job_type.all)
                _ordered[priorities[j.default_priority]] = j;
        }

        public override void set_dirty()
        {
            work_out_order();
            base.set_dirty();
        }

        public void switch_priority(job_type j1, job_type j2)
        {
            if (!j1.can_set_priority || !j2.can_set_priority) return;
            var tmp = priorities[j1.default_priority];
            priorities[j1.default_priority] = priorities[j2.default_priority];
            priorities[j2.default_priority] = tmp;
            set_dirty();
        }

        public void decrease_priority(job_type j)
        {
            if (!j.can_set_priority) return;
            byte old_priority = priorities[j.default_priority];

            // Find the job type this will be overtaking
            for (int i = 0; i < priorities.Length; ++i)
                if (priorities[i] == old_priority + 1)
                {
                    if (!job_type.all[i].can_set_priority) return;
                    priorities[i] = old_priority;
                    priorities[j.default_priority] = (byte)(old_priority + 1);
                    set_dirty();
                    return;
                }
        }

        public void increase_priority(job_type j)
        {
            if (!j.can_set_priority) return;
            byte old_priority = priorities[j.default_priority];

            // Find the job type this will be undertaking
            for (int i = 0; i < priorities.Length; ++i)
                if (priorities[i] == old_priority - 1)
                {
                    if (!job_type.all[i].can_set_priority) return;
                    priorities[i] = old_priority;
                    priorities[j.default_priority] = (byte)(old_priority - 1);
                    set_dirty();
                    return;
                }
        }

        public net_job_priorities()
        {
            // Start with default order
            for (byte i = 0; i < priorities.Length; ++i)
                priorities[i] = i;
        }

        public override byte[] serialization()
        {
            return priorities;
        }

        protected override void process_serialization(byte[] buffer, ref int offset, int length)
        {
            if (length != priorities.Length)
                throw new System.Exception("Incorrect serialization length!");
            System.Buffer.BlockCopy(buffer, offset, priorities, 0, length);
        }
    }

    public class net_skills : networked_variable
    {
        int[] xps = new int[settler.all_skills.Length];

        public delegate void on_change_func();
        public on_change_func on_change;

        public override void set_dirty()
        {
            on_change?.Invoke();
            base.set_dirty();
        }

        public int this[settler.SKILL s]
        {
            get => xps[(int)s];
            set
            {
                if (value < 0) return; // Not allowed
                if (value == this[s]) return; // No change
                xps[(int)s] = value;
                set_dirty();
            }
        }

        public override byte[] serialization()
        {
            List<byte> ret = new List<byte>();
            foreach (var i in xps)
                ret.AddRange(network_utils.encode_int(i));
            return ret.ToArray();
        }

        protected override void process_serialization(byte[] buffer, ref int offset, int length)
        {
            int index = 0;
            int end = offset + length;
            while (offset < end)
            {
                xps[index] = network_utils.decode_int(buffer, ref offset);
                index += 1;
            }

            if (index != xps.Length)
                Debug.LogError("Did not deserialize networked skills correctly!");
        }
    }
}