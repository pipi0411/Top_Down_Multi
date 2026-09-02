using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class WebGLFaviconCacheBuster : IPreprocessBuildWithReport
{
    const string TemplateIndexPath = "Assets/WebGLTemplates/TopDownNoCache/index.html";
    const string FaviconVersionPattern = "favicon\\.png(?:\\?v=[^\"']*)?";

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report == null || report.summary.platform != BuildTarget.WebGL)
            return;

        UpdateFaviconVersion();
    }

    [MenuItem("Tools/Top Down Multi/WebGL/Update Favicon Cache Version")]
    public static void UpdateFaviconVersion()
    {
        if (!File.Exists(TemplateIndexPath))
        {
            Debug.LogWarning($"[WebGLFaviconCacheBuster] Template index not found: {TemplateIndexPath}");
            return;
        }

        string version = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string html = File.ReadAllText(TemplateIndexPath);
        string updated = Regex.Replace(html, FaviconVersionPattern, $"favicon.png?v={version}");

        if (updated == html)
        {
            Debug.Log("[WebGLFaviconCacheBuster] Favicon cache version already up to date.");
            return;
        }

        File.WriteAllText(TemplateIndexPath, updated);
        AssetDatabase.ImportAsset(TemplateIndexPath);
        Debug.Log($"[WebGLFaviconCacheBuster] Updated favicon cache version: {version}");
    }
}
