using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace GcaUpdater.Services;

public sealed class FileDeployService
{
    private static readonly string[] ExcludedFileNames =
    [
        "Updater.exe",
        "Updater.dll",
        "Updater.pdb",
        "Updater.deps.json",
        "Updater.runtimeconfig.json"
    ];

    public void PrepareDirectories(UpdaterOptions options)
    {
        if (Directory.Exists(options.TempRoot))
        {
            Directory.Delete(options.TempRoot, true);
        }

        Directory.CreateDirectory(options.TempRoot);
        Directory.CreateDirectory(options.ExtractDirectory);
        Directory.CreateDirectory(options.BackupDirectory);
    }

    public string ExtractRelease(string zipPath, string extractDirectory)
    {
        ZipFile.ExtractToDirectory(zipPath, extractDirectory, overwriteFiles: true);

        var topDirs = Directory.GetDirectories(extractDirectory);
        var topFiles = Directory.GetFiles(extractDirectory);

        if (topDirs.Length == 1 && topFiles.Length == 0)
        {
            return topDirs[0];
        }

        return extractDirectory;
    }

    public void BackupCurrentInstallation(string sourceDirectory, string backupDirectory)
    {
        CopyDirectory(sourceDirectory, backupDirectory, ExcludedFileNames);
    }

    public void DeployRelease(string sourceDirectory, string targetDirectory)
    {
        CopyDirectory(sourceDirectory, targetDirectory, ExcludedFileNames);
    }

    public void RestoreBackup(string backupDirectory, string targetDirectory)
    {
        CopyDirectory(backupDirectory, targetDirectory, Array.Empty<string>());
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory, IReadOnlyCollection<string> excludedFileNames)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            if (excludedFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var relative = Path.GetRelativePath(sourceDirectory, file);
            var destination = Path.Combine(targetDirectory, relative);
            var destinationDirectory = Path.GetDirectoryName(destination);

            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(file, destination, overwrite: true);
        }
    }
}
