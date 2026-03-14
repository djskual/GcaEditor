using System;
using System.Diagnostics;
using System.IO;

namespace GcaEditor.Services;

public static class UpdateLauncher
{
    public static void LaunchUpdater(string currentVersion)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var updaterPath = Path.Combine(baseDirectory, "Updater.exe");

        if (!File.Exists(updaterPath))
        {
            throw new FileNotFoundException("Updater.exe was not found next to GcaEditor.exe.", updaterPath);
        }

        var currentProcess = Process.GetCurrentProcess();

        var args =
            $"--owner "djskual" " +
            $"--repo "GcaEditor" " +
            $"--currentVersion "{currentVersion}" " +
            $"--appDir "{baseDirectory}" " +
            $"--appExeName "GcaEditor.exe" " +
            $"--pid "{currentProcess.Id}"";

        var startInfo = new ProcessStartInfo
        {
            FileName = updaterPath,
            Arguments = args,
            WorkingDirectory = baseDirectory,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }
}
