using System.Collections.Generic;
using UnityEngine;

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
}
