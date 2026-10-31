using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildGame
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("지옥철/PC 빌드")]
    public static void Build()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        PlayerSettings.productName = "지옥철 10-4";
        PlayerSettings.companyName = "Sophie";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.usePlayerLog = true;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Windows/SubwayRush.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("[BUILD] " + report.summary.result + " errors=" + report.summary.totalErrors);
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
