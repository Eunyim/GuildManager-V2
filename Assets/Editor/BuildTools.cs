using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 배포용 빌드 메뉴. 배치 모드에서도 호출 가능:
// Unity.exe -batchmode -quit -projectPath . -executeMethod BuildTools.BuildWebGL
public static class BuildTools
{
    static string[] Scenes => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

    [MenuItem("Tools/빌드/WebGL (Builds/WebGL)")]
    public static void BuildWebGL()
    {
        // GitHub Pages는 압축 헤더를 못 붙이므로 압축 없이 빌드
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        Build(BuildTarget.WebGL, "Builds/WebGL");
    }

    [MenuItem("Tools/빌드/Windows (Builds/Windows)")]
    public static void BuildWindows()
    {
        Build(BuildTarget.StandaloneWindows64, "Builds/Windows/GuildManager.exe");
    }

    static void Build(BuildTarget target, string path)
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = path,
            target = target,
            options = BuildOptions.None,
        });
        var s = report.summary;
        Debug.Log($"[BuildTools] {target} → {s.result}, {s.totalSize / (1024 * 1024)}MB, 오류 {s.totalErrors}");
        if (Application.isBatchMode)
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }
}
