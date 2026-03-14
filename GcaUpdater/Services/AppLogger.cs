using System;
using System.IO;

namespace GcaUpdater.Services;

public sealed class AppLogger
{
    private readonly string _logFilePath;
    private readonly object _sync = new();

    public AppLogger(string logFilePath)
    {
        _logFilePath = logFilePath;

        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

        lock (_sync)
        {
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
        }
    }
}
