using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 커맨드라인(배치 모드)에서도 쓸 수 있는 프로젝트 도구.
public static class ProjectTools
{
    const string MainScenePath = "Assets/Scenes/Main.unity";
    const string BuildPath = "Builds/Windows/DungeonTamheomga.exe";
    const string IconPath = "Assets/Icon/AppIcon.png";

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

    // 앱 아이콘도 그림과 같은 방식으로 코드에서 만든다: 전사 픽셀아트를 어두운 둥근 사각형 위에 크게 찍는다.
    // 결과 PNG(Assets/Icon/AppIcon.png)는 실행 파일 아이콘이 되고, Tools\pack-store.ps1이 스토어용 크기들을 여기서 줄여 만든다.
    [MenuItem("Tools/Dungeon/Generate App Icon")]
    public static void GenerateAppIcon()
    {
        const int size = 1024, scale = 48, radius = 180, border = 28;
        var hero = PixelArt.Get(GameData.Classes[0].ArtKey).texture;   // 16x16
        var background = new Color32(0x1b, 0x18, 0x2c, 0xff);
        var gold = new Color32(0xff, 0xd9, 0x66, 0xff);

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (!InRoundedSquare(x, y, size, 0, radius)) continue;                     // 모서리는 투명
                pixels[y * size + x] = InRoundedSquare(x, y, size, border, radius - border) ? background : gold;
            }

        int heroSize = hero.width * scale, offset = (size - heroSize) / 2;
        var heroPixels = hero.GetPixels32();
        for (int y = 0; y < heroSize; y++)
            for (int x = 0; x < heroSize; x++)
            {
                var c = heroPixels[(y / scale) * hero.width + x / scale];
                if (c.a > 0) pixels[(y + offset) * size + x + offset] = c;
            }

        var icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.SetPixels32(pixels);
        icon.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
        File.WriteAllBytes(IconPath, icon.EncodeToPNG());
        Object.DestroyImmediate(icon);

        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { asset }, IconKind.Any);
        AssetDatabase.SaveAssets();
        Debug.Log($"App icon ready: {IconPath}");
    }

    // (x, y)가 한 변 size인 정사각형을 inset만큼 줄이고 모서리를 radius로 둥글린 도형 안에 있는가.
    static bool InRoundedSquare(int x, int y, int size, int inset, int radius)
    {
        int lo = inset, hi = size - 1 - inset;
        if (x < lo || x > hi || y < lo || y > hi) return false;
        int cx = Mathf.Clamp(x, lo + radius, hi - radius), cy = Mathf.Clamp(y, lo + radius, hi - radius);
        return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius;
    }

    [MenuItem("Tools/Dungeon/Build Windows")]
    public static void BuildWindows()
    {
        CreateMainScene();
        if (!File.Exists(IconPath)) GenerateAppIcon();
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
