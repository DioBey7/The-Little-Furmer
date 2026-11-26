#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// One-click web build for itch.io: Tools > Build > Web build for itch.io.
/// Output: Builds/WebGL-itch/ and a ready-to-upload Builds/TheLittleFurmer-web.zip
/// (index.html at the zip root, which is what itch.io expects).
/// Command line: Unity -batchmode -quit -projectPath . -executeMethod ItchWebGLBuild.BuildFromCommandLine
/// </summary>
public static class ItchWebGLBuild
{
    private const string OutputFolder = "Builds/WebGL-itch";
    private const string ZipPath = "Builds/TheLittleFurmer-web.zip";

    [MenuItem("Tools/Build/Web build for itch.io")]
    public static void BuildFromMenu()
    {
        if (Build() && !Application.isBatchMode)
            EditorUtility.RevealInFinder(Path.GetFullPath(ZipPath));
    }

    public static void BuildFromCommandLine()
    {
        if (!Build()) EditorApplication.Exit(1);
    }

    private static bool Build()
    {
        ApplyItchSettings();

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("[ItchWebGLBuild] No scenes in File > Build Settings.");
            return false;
        }

        if (Directory.Exists(OutputFolder)) Directory.Delete(OutputFolder, true);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputFolder,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[ItchWebGLBuild] Build failed: {report.summary.result}");
            return false;
        }

        try
        {
            if (File.Exists(ZipPath)) File.Delete(ZipPath);
            ZipFile.CreateFromDirectory(OutputFolder, ZipPath, System.IO.Compression.CompressionLevel.Optimal, false);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ItchWebGLBuild] Build OK but zipping failed ({e.Message}). Zip the contents of {OutputFolder} yourself.");
            return true;
        }

        double mb = new FileInfo(ZipPath).Length / (1024.0 * 1024.0);
        Debug.Log($"[ItchWebGLBuild] Done: {ZipPath} ({mb:0.0} MB). Upload it to itch.io as an HTML game.");
        return true;
    }

    /// <summary>Settings that make a WebGL build load correctly from itch.io's servers.</summary>
    private static void ApplyItchSettings()
    {
        // itch.io doesn't send Content-Encoding headers for Unity files, so the loader
        // must decompress in JavaScript. Gzip is quick to decompress that way; Brotli is not.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "PROJECT:Itch";
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.showDiagnostics = false;

        // Smaller, faster code for release.
        PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL, Il2CppCompilerConfiguration.Master);
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);

        // Unity's splash screen can't be removed on a Personal license; leave it as configured.
    }
}
#endif
