using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBuildTools
{
    private const string GradleExportRelativePath = "Builds/Android";
    private const string ApkBuildRelativePath = "Builds/AndroidApk";

    [MenuItem("Tools/Rogue Slide/Build/Export Android Gradle Project")]
    public static void ExportAndroidGradleProject()
    {
        RunAndroidBuild(exportGradleProject: true);
    }

    [MenuItem("Tools/Rogue Slide/Build/Build Android APK")]
    public static void BuildAndroidApk()
    {
        RunAndroidBuild(exportGradleProject: false);
    }

    public static void ExportAndroidGradleProjectBatch()
    {
        RunAndroidBuild(exportGradleProject: true, batchMode: true);
    }

    public static void BuildAndroidApkBatch()
    {
        RunAndroidBuild(exportGradleProject: false, batchMode: true);
    }

    private static void RunAndroidBuild(bool exportGradleProject, bool batchMode = false)
    {
        string[] enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (enabledScenes.Length == 0)
        {
            FailBuild("No enabled scenes found in Build Settings.", batchMode);
            return;
        }

        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(projectRoot))
        {
            FailBuild("Unable to resolve the project root folder.", batchMode);
            return;
        }

        string outputPath = exportGradleProject
            ? Path.Combine(projectRoot, GradleExportRelativePath)
            : Path.Combine(projectRoot, ApkBuildRelativePath, $"{PlayerSettings.productName}.apk");

        string outputDirectory = exportGradleProject ? outputPath : Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(outputDirectory))
        {
            FailBuild($"Unable to resolve the output directory for '{outputPath}'.", batchMode);
            return;
        }

        bool previousExportAsGradleProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        BuildTarget previousBuildTarget = EditorUserBuildSettings.activeBuildTarget;

        try
        {
            PrepareOutputDirectory(outputDirectory);

            if (previousBuildTarget != BuildTarget.Android)
            {
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                if (!switched)
                {
                    FailBuild("Unity could not switch the active build target to Android.", batchMode);
                    return;
                }
            }

            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = exportGradleProject;

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = enabledScenes,
                target = BuildTarget.Android,
                locationPathName = outputPath,
                options = BuildOptions.None
            };

            Debug.Log($"Starting Android build. ExportGradleProject={exportGradleProject}, Output='{outputPath}'");
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                FailBuild($"Android build failed with result {report.summary.result}.", batchMode);
                return;
            }

            string successMessage = exportGradleProject
                ? $"Android Gradle project exported successfully to '{outputPath}'."
                : $"Android APK built successfully at '{outputPath}'.";

            Debug.Log(successMessage);
        }
        catch (Exception exception)
        {
            FailBuild($"Android build failed with exception: {exception}", batchMode);
        }
        finally
        {
            EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExportAsGradleProject;
        }
    }

    private static void PrepareOutputDirectory(string outputDirectory)
    {
        if (Directory.Exists(outputDirectory))
        {
            FileUtil.DeleteFileOrDirectory(outputDirectory);
            FileUtil.DeleteFileOrDirectory($"{outputDirectory}.meta");
        }

        Directory.CreateDirectory(outputDirectory);
    }

    private static void FailBuild(string message, bool batchMode)
    {
        Debug.LogError(message);
        if (batchMode)
        {
            throw new BuildFailedException(message);
        }
    }
}
