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

/// <summary> A value serialized over the network. </summary>
public abstract class networked_variable
{
    /// <summary> Serialize my value into a form suitable
    /// for sending over the network </summary>
    public abstract byte[] serialization();

    /// <summary> Reconstruct my value from the result of
    /// <see cref="serialization"/>. </summary>
    public void deserialize(byte[] buffer, int offset, int length)
    {
        on_deserialize(buffer, offset, length, first_deserialize);
        first_deserialize = false;
    }
    bool first_deserialize = true;

    public abstract void on_deserialize(byte[] buffer, int offset, int length, bool first_time);

    /// <summary> Called when a variable update
    /// needs to be sent to the server. </summary>
    protected void send_update()
    {
        queued_serial = serialization();
    }
    public byte[] queued_serial;

    /// <summary> A simple networked integer. </summary>
    public class net_int : networked_variable
    {
        public int value
        {
            get => _value;
            set
            {
                if (_value == value)
                    return; // No change

                _value = value;
                on_change?.Invoke(_value, false);
                send_update();
            }
        }
        int _value;

        public net_int() { }
        public net_int(int init) { _value = init; }

        public override byte[] serialization()
        {
            return network_utils.encode_int(value);
        }

        public override void on_deserialize(byte[] buffer, int offset, int length, bool first_time)
        {
            _value = network_utils.decode_int(buffer, ref offset);
            on_change?.Invoke(_value, true);
        }

        public delegate void change_func(int new_value, bool first_deserialize);
        public change_func on_change;
    }

    /// <summary> A networked string. </summary>
    public class net_string : networked_variable
    {
        public string value
        {
            get => _value;
            set
            {
                if (value == null)
                    value = ""; // Can't serialize null strings

                if (_value == value)
                    return; // No change

                _value = value;
                send_update();
            }
        }
        string _value = ""; // Can't serialize null strings

        public override byte[] serialization()
        {
            return network_utils.encode_string(_value);
        }

        public override void on_deserialize(byte[] buffer, int offset, int length, bool first_time)
        {
            _value = network_utils.decode_string(buffer, ref offset);
        }
    }

    /// <summary> A networked floating point number, supporting resolution + lerp. </summary>
    public class net_float : networked_variable
    {
        /// <summary> The most up-to-date value we have. </summary>
        public float value
        {
            get => _value;
            set
            {
                if (_value == value)
                    return; // No change

                _value = value;
                on_change?.Invoke(_value, false);

                // Only send network updates if we've
                // moved by more than the resolution
                if (Mathf.Abs(_last_sent - _value) > resolution)
                {
                    _last_sent = _value;
                    send_update();
                }
            }
        }
        float _value;
        float _last_sent;

        /// <summary> A smoothed value. </summary>
        public float lerped_value
        {
            get
            {
                _lerp_value = Mathf.Lerp(_lerp_value, _value, Time.deltaTime * lerp_speed);
                return _lerp_value;
            }
        }
        float _lerp_value;

        /// <summary> Sets the lerped value = networked value 
        /// (i.e fast-forwards the lerping to completion) </summary>
        public void reset_lerp()
        {
            _lerp_value = _value;
        }

        float lerp_speed;
        float resolution;

        public net_float(float init = 0f, float lerp_speed = 5f, float resolution = 0f)
        {
            _value = init;
            _lerp_value = init;
            this.lerp_speed = lerp_speed;
            this.resolution = resolution;
        }

        public override byte[] serialization()
        {
            return network_utils.encode_float(value);
        }

        public override void on_deserialize(byte[] buffer, int offset, int length, bool first_time)
        {
            _value = network_utils.decode_float(buffer, ref offset);
            if (first_time) reset_lerp(); // Initialize the lerp to the exact value first time
            on_change?.Invoke(_value, first_time);
        }

        public delegate void change_func(float new_value, bool first_deserialize);
        public change_func on_change;
    }

    public class net_quaternion : networked_variable
    {
        public Quaternion value
        {
            get => value;
            set
            {
                if (_value == value)
                    return; // No change
                _value = value;
                on_change?.Invoke(_value, false);
                send_update();
            }
        }
        Quaternion _value = Quaternion.identity;

        public override byte[] serialization()
        {
            return network_utils.concat_buffers(
                network_utils.encode_float(_value.x),
                network_utils.encode_float(_value.y),
                network_utils.encode_float(_value.z),
                network_utils.encode_float(_value.w)
            );
        }

        public override void on_deserialize(byte[] buffer, int offset, int length, bool first_time)
        {
            _value = new Quaternion(
                network_utils.decode_float(buffer, ref offset),
                network_utils.decode_float(buffer, ref offset),
                network_utils.decode_float(buffer, ref offset),
                network_utils.decode_float(buffer, ref offset)
            );
            on_change?.Invoke(_value, first_time);
        }

        public delegate void change_func(Quaternion new_value, bool first_deserialize);
        public change_func on_change;
    }

    /// <summary> Represents a map from strings to ints. </summary>
    public class net_string_counts : networked_variable, IEnumerable<KeyValuePair<string, int>>
    {
        public object this[string str]
        {
            get => dict[str];
            set
            {
                int i = (int)value;

                int got;
                if (dict.TryGetValue(str, out got))
                    if (i == got)
                        return; // No change


                if (i == 0) dict.Remove(str);
                else dict[str] = i;

                on_change?.Invoke();
                send_update();
            }

        }
        SortedDictionary<string, int> dict = new SortedDictionary<string, int>();

        public void set(Dictionary<string, int> counts)
        {
            // Chek we have the same number of item types
            bool different = dict.Count != counts.Count;

            // Check the items we have are the same + in the same quantities
            if (!different)
                foreach (var kv in counts)
                    if (!dict.ContainsKey(kv.Key) || dict[kv.Key] != kv.Value)
                    {
                        different = true;
                        break;
                    }

            if (!different)
                return;

            dict.Clear();
            foreach (var kv in counts)
                dict[kv.Key] = kv.Value;

            on_change?.Invoke();
            send_update();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            return dict.GetEnumerator();
        }

        public override byte[] serialization()
        {
            List<byte> ret = new List<byte>();
            foreach (var kv in dict)
            {
                ret.AddRange(network_utils.encode_string(kv.Key));
                ret.AddRange(network_utils.encode_int(kv.Value));
            }
            return ret.ToArray();
        }

        public override void on_deserialize(byte[] buffer, int offset, int length, bool first_time)
        {
            dict.Clear();

            int end = offset + length;
            while (offset < end)
            {
                string key = network_utils.decode_string(buffer, ref offset);
                int value = network_utils.decode_int(buffer, ref offset);
                dict[key] = value;
            }

            on_change?.Invoke();
        }

        public delegate void change_func();
        public change_func on_change;
    }
}