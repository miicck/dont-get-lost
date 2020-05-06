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
    public abstract void deserialize(byte[] buffer, int offset, int length);

    /// <summary> Called when the object we belong to has been created. </summary>
    public virtual void on_create() { }

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
                on_change?.Invoke(_value);
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

        public override void deserialize(byte[] buffer, int offset, int length)
        {
            _value = network_utils.decode_int(buffer, ref offset);
            on_change?.Invoke(_value);
        }

        public delegate void change_func(int new_value);
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

        public override void deserialize(byte[] buffer, int offset, int length)
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
                on_change?.Invoke(_value);

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

        public override void on_create()
        {
            // Start lerp value at initial value
            _lerp_value = _value;
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

        public override void deserialize(byte[] buffer, int offset, int length)
        {
            _value = network_utils.decode_float(buffer, ref offset);
            on_change?.Invoke(_value);
        }

        public delegate void change_func(float new_value);
        public change_func on_change;
    }
}