using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class facial_expression : MonoBehaviour
{
    public enum EXPRESSION
    {
        NEUTRAL
    }

    public EXPRESSION expression
    {
        get => _expression;
        set
        {
            rend.material = load(value);
            _expression = value;
        }
    }
    EXPRESSION _expression;

    bool blinking
    {
        get => _blinking;
        set
        {
            if (_blinking == value) return;
            _blinking = value;
            expression = expression;
        }
    }
    bool _blinking;

    Renderer rend;

    private void Start()
    {
        rend = GetComponent<Renderer>();
        expression = EXPRESSION.NEUTRAL;
        Invoke("blink_cycle", Random.Range(0, 2f));
    }

    void blink_cycle()
    {
        blinking = !blinking;
        if (blinking) Invoke("blink_cycle", Random.Range(0.1f, 0.3f));
        else Invoke("blink_cycle", Random.Range(0.2f, 1.5f));
    }

    Material load(EXPRESSION e)
    {
        string name = "materials/faces/" + e.ToString().ToLower();
        if (blinking) name += "_blink";
        var ret = Resources.Load<Material>(name);
        if (ret == null) Debug.LogError("Unkown face material: " + name);
        return ret;
    }
}
