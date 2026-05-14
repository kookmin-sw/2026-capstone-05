using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class AuthApiBaseUrlBuildPostprocessor : IPostprocessBuildWithReport
{
    private const string ConfigFileName = "auth-api-base-url.txt";

    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report == null)
            return;

        string buildDirectory = GetBuildDirectory(report.summary.outputPath);
        if (string.IsNullOrWhiteSpace(buildDirectory))
            return;

        string targetPath = Path.Combine(buildDirectory, ConfigFileName);
        string sourcePath = GetSourceConfigPath();
        string content = File.Exists(sourcePath)
            ? File.ReadAllText(sourcePath)
            : "http://localhost:8080";

        Directory.CreateDirectory(buildDirectory);
        File.WriteAllText(targetPath, content.Trim() + "\n");
        Debug.Log($"[Build] Wrote Auth API URL config: {targetPath}");
    }

    private static string GetBuildDirectory(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return null;

        string extension = Path.GetExtension(outputPath);
        if (!string.IsNullOrWhiteSpace(extension))
            return Path.GetDirectoryName(outputPath);

        return outputPath;
    }

    private static string GetSourceConfigPath()
    {
        string projectRootConfig = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ConfigFileName));
        if (File.Exists(projectRootConfig))
            return projectRootConfig;

        return Path.Combine(Application.streamingAssetsPath, ConfigFileName);
    }
}
