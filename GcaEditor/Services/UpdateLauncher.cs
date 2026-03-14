using System.Diagnostics;
using System.IO;

namespace GcaEditor.Services;

public static class UpdateLauncher
{
    public static void LaunchUpdater(string currentVersion)
    {
        var updaterPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Updater.exe");

        if (!File.Exists(updaterPath))
        {
            throw new FileNotFoundException("Updater.exe not found.", updaterPath);
        }

        var args =
            $"--owner \"djskual\" " +
            $"--repo \"GcaEditor\" " +
            $"--currentVersion \"{currentVersion}\"";

        var psi = new ProcessStartInfo
        {
            FileName = updaterPath,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(updaterPath)!,
            UseShellExecute = true
        };

        Process.Start(psi);
    }
}