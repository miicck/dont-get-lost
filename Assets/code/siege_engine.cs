using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class siege_engine : MonoBehaviour, IAddsToInspectionText
{
    public enum FIRE_STAGE
    {
        SEARCHING_FOR_TARGET,
        PREPARING_TO_FIRE,
        FIRING
    }

    public FIRE_STAGE stage
    {
        get => _stage;
        private set
        {
            if (_stage == value) return; // No change
            stage_progress = 0f;
            var old_stage = _stage;
            _stage = value;

            on_stage_change(old_stage, _stage);
        }
    }
    FIRE_STAGE _stage;

    public character target { get; private set; } = null;

    float stage_progress = 0f;

    protected virtual void Update()
    {
        stage_progress += Time.deltaTime;

        if (target == null || target.is_dead)
            stage = FIRE_STAGE.SEARCHING_FOR_TARGET;

        if (stage == FIRE_STAGE.SEARCHING_FOR_TARGET)
        {
            // Only search for target every 0.1 seconds
            if ((int)(stage_progress * 10f) % 10 == 0)
            {
                // Seach for nearest target
                target = group_info.closest_attacker(transform.position);
                if (target != null)
                    stage = FIRE_STAGE.PREPARING_TO_FIRE;
            }
        }

        if (stage_update(stage, stage_progress))
        {
            // Increment stage (looping)
            int next_stage = (int)(stage + 1);
            if (System.Enum.IsDefined(typeof(FIRE_STAGE), next_stage))
                stage = (FIRE_STAGE)next_stage;
            else
                stage = FIRE_STAGE.SEARCHING_FOR_TARGET;
        }
    }

    protected abstract bool stage_update(FIRE_STAGE stage, float progress);
    protected virtual void on_stage_change(FIRE_STAGE from, FIRE_STAGE to) { }

    public string added_inspection_text() => "Firing stage: " + stage.ToString().ToLower().Replace('_', ' ');
}
