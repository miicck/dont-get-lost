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

        public net_float(float lerp_speed = 5f, float resolution = 0f)
        {
            this.lerp_speed = lerp_speed;
            this.resolution = resolution;
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
}