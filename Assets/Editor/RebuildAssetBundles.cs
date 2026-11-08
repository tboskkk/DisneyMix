using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RebuildAssetBundles
{
    private static readonly string OutputRoot = Path.Combine(Application.streamingAssetsPath, "AssetBundles");
    private const string RawBuildFolderName = "_raw";
    private const string AssetBundlesJsonRelativePath = "Assets/Resources/json/AssetBundles.json";

    [Serializable]
    private class ManifestData
    {
        public ManifestContent manifest;
    }

    [Serializable]
    private class ManifestContent
    {
        // Note: JsonUtility doesn't support Dictionaries directly. 
        // We will read the raw text for the 'paths' section to stay flexible.
        public string version;
    }

    [MenuItem("Tools/AssetBundles/Rebuild from JSON")]
    public static void RebuildAll()
    {
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string platformFolder = GetPlatformFolder(target);
        string rawBuildPath = Path.Combine(OutputRoot, RawBuildFolderName);

        // 1. Setup Directories
        // Only this platform's folder is cleared, so rebuilding one platform
        // keeps the bundles already built for the others (windows64 ships
        // prebuilt in the repo).
        string platformRoot = Path.Combine(OutputRoot, platformFolder);
        if (Directory.Exists(platformRoot)) Directory.Delete(platformRoot, true);
        if (Directory.Exists(rawBuildPath)) Directory.Delete(rawBuildPath, true);
        Directory.CreateDirectory(rawBuildPath);

        // 2. Build Bundles
        // Unity builds files into 'rawBuildPath' using the Bundle Name.
        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(rawBuildPath, BuildAssetBundleOptions.ForceRebuildAssetBundle, target);

        if (manifest == null)
        {
            Debug.LogError("AssetBundle build failed.");
            return;
        }

        // 3. Parse JSON for Mapping
        string jsonPath = Path.Combine(Directory.GetCurrentDirectory(), AssetBundlesJsonRelativePath);
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"JSON Manifest missing at: {jsonPath}");
            return;
        }

        string jsonContent = File.ReadAllText(jsonPath);

        // Extracting paths using a simple loop over the "paths" keys in the JSON.
        // Since your JSON is a dictionary of paths, we iterate through the keys.
        string searchPattern = "\"AssetBundles/(.*?)\":";
        var matches = System.Text.RegularExpressions.Regex.Matches(jsonContent, searchPattern);

        int movedCount = 0;
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            string relativePath = match.Groups[1].Value; // e.g. platform/hd/avatar/.../file.unity3d

            // Skip system files like .DS_Store
            if (relativePath.EndsWith(".DS_Store")) continue;

            // Resolve the "platform" token to the current build target
            string targetPath = relativePath.Replace("platform", platformFolder);
            string fileName = Path.GetFileName(targetPath);

            string sourceFile = Path.Combine(rawBuildPath, fileName);
            string destFile = Path.Combine(OutputRoot, targetPath);

            if (File.Exists(sourceFile))
            {
                string destDir = Path.GetDirectoryName(destFile);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                // Move from flat _raw folder to structured folder
                File.Move(sourceFile, destFile);
                movedCount++;
            }
        }

        // 4. Cleanup
        if (Directory.Exists(rawBuildPath)) Directory.Delete(rawBuildPath, true);

        AssetDatabase.Refresh();
        Debug.Log($"Successfully organized {movedCount} bundles based on AssetBundles.json for {platformFolder}.");
    }

    // Batchmode entry point (-executeMethod RebuildAssetBundles.RebuildAllBatch).
    // RebuildAll reports failures with Debug.LogError and returns, so under
    // -quit the editor would still exit 0. This exits 1 when it throws or
    // leaves no bundles for the active platform.
    public static void RebuildAllBatch()
    {
        string platformRoot = Path.Combine(OutputRoot, GetPlatformFolder(EditorUserBuildSettings.activeBuildTarget));
        try
        {
            RebuildAll();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
            return;
        }
        if (!Directory.Exists(platformRoot) || Directory.GetFiles(platformRoot, "*.unity3d", SearchOption.AllDirectories).Length == 0)
        {
            Debug.LogError($"No bundles were written to {platformRoot}.");
            EditorApplication.Exit(1);
            return;
        }
        EditorApplication.Exit(0);
    }

    private static string GetPlatformFolder(BuildTarget target)
    {
        return target switch
        {
            BuildTarget.Android => "android",
            BuildTarget.StandaloneWindows64 => "windows64",
            BuildTarget.iOS => "iphone",
            _ => target.ToString().ToLowerInvariant()
        };
    }
}