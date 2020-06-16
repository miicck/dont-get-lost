using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class water_reflections : MonoBehaviour
{
    UnityEngine.Rendering.HighDefinition.PlanarReflectionProbe probe;
    Renderer water_renderer;
    Renderer water_underside_renderer;

    /// <summary> The water quad is centred this distance from the player, so that
    /// it appears behind transparent objects that are nearer. </summary>
    const float WATER_CENTRE_OFFSET = 64f;

    const float WATER_REFLECTION_RANGE = 64f;
    const float WATER_BLEND_RANGE = WATER_REFLECTION_RANGE / 2f;

    void Start()
    {
        // Create the water reflection probe
        probe = Resources.Load
            <UnityEngine.Rendering.HighDefinition.PlanarReflectionProbe>
            ("misc/water_reflection_probe").inst();
        probe.transform.position = transform.position + transform.forward * 16f;
        probe.transform.rotation = transform.rotation;
        probe.transform.SetParent(transform);

        /*
        probe.influenceVolume.shape = UnityEngine.Rendering.HighDefinition.InfluenceShape.Sphere;
        probe.influenceVolume.sphereRadius = WATER_REFLECTION_RANGE;
        probe.influenceVolume.sphereBlendDistance = WATER_BLEND_RANGE;
        */

        probe.influenceVolume.shape = UnityEngine.Rendering.HighDefinition.InfluenceShape.Box;
        probe.influenceVolume.boxSize = new Vector3(WATER_REFLECTION_RANGE, 0.01f, WATER_REFLECTION_RANGE);
        probe.influenceVolume.boxBlendDistanceNegative = new Vector3(
            WATER_BLEND_RANGE, 0.005f, WATER_BLEND_RANGE
        );
        probe.influenceVolume.boxBlendDistancePositive = new Vector3(
            WATER_BLEND_RANGE, 0.005f, WATER_BLEND_RANGE
        );

        // Create the water
        var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(water.GetComponent<Collider>());
        water.transform.SetParent(transform);

        water.transform.localRotation = Quaternion.identity;
        water.transform.localPosition = new Vector3(0, 0, WATER_CENTRE_OFFSET);
        water.transform.rotation = Quaternion.LookRotation(Vector3.down, transform.right);

        water_renderer = water.gameObject.GetComponent<MeshRenderer>();
        water_renderer.material = Resources.Load<Material>("materials/standard_shader/water_reflective");
        water_renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var water_underside = water.inst();
        water_underside.transform.SetParent(water.transform);
        water_underside.transform.localPosition = new Vector3(0, 0, -0.00001f);
        water_underside.transform.Rotate(180, 0, 0);
        water_underside_renderer = water_underside.GetComponent<MeshRenderer>();
        water_underside_renderer.material = Resources.Load<Material>("materials/standard_shader/water_underside");

        // Needs to be fiddled with on startup to work for some reason
        fiddle_needed = true;
        last_fiddled = player.current.transform.position;
    }

    bool fiddle_needed = false;
    Vector3 last_fiddled;

    public Color color = water_colors.cyan;

    void Update()
    {
        water_underside_renderer.enabled =
            player.current.camera.transform.position.y < world.SEA_LEVEL;

        // Workaround to stop the reflections from just randomly disapearing
        // if the player moves too far.
        if ((last_fiddled - player.current.transform.position).magnitude >
             probe.influenceVolume.sphereRadius)
        {
            fiddle_needed = true;
            last_fiddled = player.current.transform.position;
        }

        bool should_reflect = reflections_enabled && !player.current.map_open;

        if (fiddle_needed)
        {
            fiddle_needed = false;
            probe.enabled = false;
        }

        if (probe.enabled != should_reflect)
        {
            probe.enabled = should_reflect;
            water_renderer.material = Resources.Load<Material>(
                should_reflect ?
                "materials/standard_shader/water_reflective" :
                "materials/standard_shader/water_normal"
                );
        }

        color.a = probe.enabled ? 0.84f : 0.31f;
        utils.set_color(water_renderer.material, color);

        // Ensure probe stays at water level
        Vector3 pos = transform.position;
        pos.y = world.SEA_LEVEL;
        transform.position = pos;

        water_renderer.transform.localScale = Vector3.one *
            (water_range + WATER_CENTRE_OFFSET) * 2f;
    }

    public static float water_range = 256;
    public static bool reflections_enabled = true;
}
