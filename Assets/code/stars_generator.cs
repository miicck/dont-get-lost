using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class stars_generator : MonoBehaviour
{
    Material star_material;

    void Start()
    {
        var star_template = Resources.Load<GameObject>("stars/simple_star");
        star_material = Resources.Load<Material>("materials/star");

        for (int i = 0; i < 1000; ++i)
        {

            float phi = Random.Range(0, 2 * Mathf.PI);
            float theta = Random.Range(0, Mathf.PI);

            Vector3 pos = Random.onUnitSphere * 0.99f;
            if (pos.y < 0) continue;

            var star = star_template.inst();

            star.transform.SetParent(transform);
            star.transform.localPosition = pos;
            star.transform.forward = pos;
            star.transform.localScale = Vector3.one * Random.Range(0.4f, 1f) * 0.01f;
            star.GetComponentInChildren<Renderer>().sharedMaterial = star_material;
        }
    }

    private void Update()
    {
        float alpha = 1f - time_manager.time_to_brightness;
        alpha = alpha * alpha;
        var color = new Color(1, 1, 1, alpha);
        utils.set_color(star_material, color);
    }
}
