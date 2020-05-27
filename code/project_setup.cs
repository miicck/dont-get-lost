using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Utility class to run project setup
public class project_setup : MonoBehaviour
{
    static void setup()
    {
        string log = "Running project setup...\n";

        UnityEditor.PlayerSettings.resizableWindow = true;
        log += "Resizable window: " + UnityEditor.PlayerSettings.resizableWindow + "\n";

        UnityEditor.PlayerSettings.companyName = "mick\n";
        log += "Company name: " + UnityEditor.PlayerSettings.companyName;

        UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset =
            Resources.Load<UnityEngine.Rendering.RenderPipelineAsset>("pipeline/pipeline_settings");
        log += "Pipeline settings: " + UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset.name;

        UnityEditor.EditorSettings.serializationMode = UnityEditor.SerializationMode.ForceText;
        log += "Editor serializaton mode: " + UnityEditor.EditorSettings.serializationMode + "\n";

        UnityEditor.EditorSettings.externalVersionControl = "Visible Meta Files";
        log += "External version control: " + UnityEditor.EditorSettings.externalVersionControl + "\n";

        Debug.Log(log);
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(project_setup))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (UnityEditor.EditorGUILayout.Toggle("Run setup", false))
                setup();
        }
    }
#endif
}
