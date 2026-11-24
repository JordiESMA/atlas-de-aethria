#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;

public static class AtlasVisualReviewBuild
{
    public static void Build()
    {
        string file=Path.GetFullPath("Build/VisualReview/AtlasReview.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/Scenes/Main.unity"},locationPathName=file,
            target=BuildTarget.StandaloneWindows64,
            extraScriptingDefines=new[]{"ATLAS_VISUAL_QA"}
        });
        if(report.summary.result!=BuildResult.Succeeded) throw new System.Exception("Visual review build failed");
    }
}
#endif
