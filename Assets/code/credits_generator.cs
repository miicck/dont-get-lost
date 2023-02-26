using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class credits_generator : MonoBehaviour
{
    public UnityEngine.UI.Text git_contributors;

    private void Start()
    {
        git_contributors.text = version_control.info.contributors;
    }
}
