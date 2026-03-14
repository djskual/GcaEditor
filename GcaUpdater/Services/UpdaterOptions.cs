using System;
using System.Collections.Generic;
using System.IO;

namespace GcaUpdater.Services;

public sealed class UpdaterOptions
{
    public string Owner { get; init; } = "djskual";
    public string Repo { get; init; } = "GcaEditor";
    public string CurrentVersion { get; init; } = "v0.0.0";
    public string AppDirectory { get; init; } = AppDomain.CurrentDomain.BaseDirectory;
    public string AppExeName { get; init; } = "GcaEditor.exe";
    public int? ProcessIdToWait { get; init; }
    public string TempRoot { get; init; } = Path.Combine(Path.GetTempPath(), "GcaEditorUpdater");
    public string LogFilePath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GcaEditor",
        "Logs",
        "updater.log");

    public static UpdaterOptions Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = arg[2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";

            map[key] = value;
        }

        var appDir = map.TryGetValue("appDir", out var explicitDir)
            ? explicitDir
            : AppDomain.CurrentDomain.BaseDirectory;

        return new UpdaterOptions
        {
            Owner = map.TryGetValue("owner", out var owner) ? owner : "djskual",
            Repo = map.TryGetValue("repo", out var repo) ? repo : "GcaEditor",
            CurrentVersion = map.TryGetValue("currentVersion", out var version) ? version : "v0.0.0",
            AppDirectory = appDir,
            AppExeName = map.TryGetValue("appExeName", out var exeName) ? exeName : "GcaEditor.exe",
            ProcessIdToWait = map.TryGetValue("pid", out var pidRaw) && int.TryParse(pidRaw, out var pid) ? pid : null,
            TempRoot = map.TryGetValue("tempRoot", out var tempRoot)
                ? tempRoot
                : Path.Combine(Path.GetTempPath(), "GcaEditorUpdater"),
            LogFilePath = map.TryGetValue("logFile", out var logFile)
                ? logFile
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GcaEditor",
                    "Logs",
                    "updater.log")
        };
    }

    public string AppExePath => Path.Combine(AppDirectory, AppExeName);
    public string ZipPath => Path.Combine(TempRoot, "package.zip");
    public string ExtractDirectory => Path.Combine(TempRoot, "extracted");
    public string BackupDirectory => Path.Combine(TempRoot, "backup");
}
