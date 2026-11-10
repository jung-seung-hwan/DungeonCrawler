using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 커맨드라인(배치 모드)에서도 쓸 수 있는 프로젝트 도구.
public static class ProjectTools
{
    const string MainScenePath = "Assets/Scenes/Main.unity";
    const string BuildPath = "Builds/Windows/DungeonTamheomga.exe";

    [MenuItem("Tools/Dungeon/Create Main Scene")]
    public static void CreateMainScene()
    {
        if (!File.Exists(MainScenePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MainScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, MainScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
        PlayerSettings.productName = "DungeonTamheomga";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.runInBackground = true; // 창이 포커스를 잃어도 멈추지 않게(자동 테스트용)
        AssetDatabase.SaveAssets();
        Debug.Log($"Main scene ready: {MainScenePath}");
    }

    [MenuItem("Tools/Dungeon/Build Windows")]
    public static void BuildWindows()
    {
        CreateMainScene();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { MainScenePath },
            locationPathName = BuildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });

        bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        Debug.Log($"Build {(ok ? "succeeded" : "failed")}: {BuildPath}");
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }
}
