#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AtlasBuild
{
    [MenuItem("Atlas/Build Windows Player")]
    public static void BuildWindowsPlayer()
    {
        string hostSource = Path.GetFullPath("server/index.js");
        string hostFolder = Path.GetFullPath("Assets/StreamingAssets/P2PHost");
        Directory.CreateDirectory(hostFolder);
        if (!File.Exists(Path.Combine(hostFolder, "node.exe")))
            throw new System.Exception("Bundled Node runtime missing: Assets/StreamingAssets/P2PHost/node.exe");
        File.Copy(hostSource, Path.Combine(hostFolder, "index.js"), true);
        string output = Path.GetFullPath("Build/Candidate/AtlasDeAethria");
        Directory.CreateDirectory(output);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = Path.Combine(output, "Atlas de Aethria.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        Debug.Log("ATLAS_BUILD_RESULT=" + report.summary.result + " PATH=" + output);
        if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Atlas Windows build failed: " + report.summary.result);
    }
}
#endif
