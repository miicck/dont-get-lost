using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class job_panel : MonoBehaviour
{
    public UnityEngine.UI.Text job_name;
    public UnityEngine.UI.Toggle enabled_toggle;
    public UnityEngine.UI.Button increase_priority;
    public UnityEngine.UI.Button decrease_priority;
    public UnityEngine.UI.Text skill_level_lower;
    public UnityEngine.UI.Text skill_level_upper;
    public UnityEngine.UI.Image skill_progress;
    public skill job_type;
}