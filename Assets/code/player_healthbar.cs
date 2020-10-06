using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player_healthbar : MonoBehaviour
{
    public enum TYPE
    {
        HEALTH,
        FOOD,
    }

    public TYPE type;
    public UnityEngine.UI.Image red;
    public UnityEngine.UI.Image green;
    public UnityEngine.UI.Text text;

    /// <summary> Set the value of the healtbar to health/max_health. </summary>
    public void set(int health, int max_health)
    {
        if (health > max_health) throw new System.Exception("health > max_health!");
        if (health < 0) throw new System.Exception("health < 0!");

        text.text = health + "/" + max_health;
        float right = red.rectTransform.rect.width * (max_health - health) / max_health;
        green.rectTransform.offsetMax = new Vector2(-right, green.rectTransform.offsetMax.y);
    }
}