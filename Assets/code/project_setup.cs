using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR

// Utility class to run project setup
public class project_setup : MonoBehaviour
{
    public List<UnityEditor.SceneAsset> scenes_in_build = new List<UnityEditor.SceneAsset>();

    void setup()
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

        UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
        log += "External version control: " + UnityEditor.VersionControlSettings.mode + "\n";

        var scenes = new List<UnityEditor.EditorBuildSettingsScene>();
        foreach (var s in scenes_in_build)
            scenes.Add(new UnityEditor.EditorBuildSettingsScene(
                UnityEditor.AssetDatabase.GetAssetPath(s), true));
        UnityEditor.EditorBuildSettings.scenes = scenes.ToArray();
        log += "Added " + scenes.Count + " scenes to build\n";

        Debug.Log(log);
    }

    [UnityEditor.CustomEditor(typeof(project_setup))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (UnityEditor.EditorGUILayout.Toggle("Run setup", false))
                ((project_setup)target).setup();
        }
    }

}

#endif
