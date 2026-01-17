using System.Collections.Generic;
using UnityEngine;
using TMPro;

internal sealed class ThirdPersonSetupReport
{
    private readonly List<string> _infos = new List<string>();
    private readonly List<string> _warnings = new List<string>();
    private readonly List<string> _errors = new List<string>();

    public int WarningCount => _warnings.Count;
    public int ErrorCount => _errors.Count;

    public void AddInfo(string message) => _infos.Add(message);
    public void AddWarning(string message) => _warnings.Add(message);
    public void AddError(string message) => _errors.Add(message);

    public void PrintSummary()
    {
        if (_errors.Count == 0 && _warnings.Count == 0 && _infos.Count == 0)
        {
            Debug.Log("ThirdPerson setup report: no notes.");
            return;
        }

        Debug.Log($"ThirdPerson setup report: {_infos.Count} info, {_warnings.Count} warnings, {_errors.Count} errors.");

        foreach (string message in _infos)
        {
            Debug.Log($"[Setup] {message}");
        }

        foreach (string message in _warnings)
        {
            Debug.LogWarning($"[Setup] {message}");
        }

        foreach (string message in _errors)
        {
            Debug.LogError($"[Setup] {message}");
        }
    }
}

public partial class ThirdPersonSetup
{
    private static ThirdPersonSetupReport _report;

    private static ThirdPersonSetupReport Report
    {
        get
        {
            if (_report == null)
            {
                _report = new ThirdPersonSetupReport();
            }

            return _report;
        }
    }

    private static void ResetReport()
    {
        _report = new ThirdPersonSetupReport();
    }

    private static void ReportInfo(string message) => Report.AddInfo(message);
    private static void ReportWarning(string message) => Report.AddWarning(message);
    private static void ReportError(string message) => Report.AddError(message);
    private static void PrintReportSummary() => Report.PrintSummary();

    private static void EnsureTmpSettingsAsset()
    {
        const string tmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        const string defaultFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        TMP_Settings settingsAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_Settings>(tmpSettingsPath);
        if (settingsAsset == null)
        {
            ReportWarning("TextMeshPro settings asset missing. Import TMP Essentials or assign TMP Settings in Project Settings.");
            return;
        }

        TMP_FontAsset defaultFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(defaultFontPath);
        if (TMP_Settings.defaultFontAsset == null && defaultFont != null)
        {
            TMP_Settings.defaultFontAsset = defaultFont;
            if (TMP_Settings.fallbackFontAssets == null)
            {
                TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();
            }

            UnityEditor.EditorUtility.SetDirty(settingsAsset);
            UnityEditor.AssetDatabase.SaveAssets();
            ReportInfo("Assigned TMP default font asset (LiberationSans SDF).");
        }
        else if (TMP_Settings.defaultFontAsset == null)
        {
            ReportWarning("TMP Settings has no default font asset assigned.");
        }

        EnsureTmpTextComponents();
    }

    private static void EnsureTmpTextComponents()
    {
        TMPro.TextMeshPro[] texts = UnityEngine.Object.FindObjectsByType<TMPro.TextMeshPro>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        if (texts == null || texts.Length == 0)
        {
            return;
        }

        foreach (TMPro.TextMeshPro text in texts)
        {
            if (text == null || text.font != null)
            {
                continue;
            }

            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
                UnityEditor.EditorUtility.SetDirty(text);
            }
        }
    }
}
