using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GcaUpdater.Services;

public sealed class ProcessService
{
    public async Task WaitForExitAsync(int? pid, string appExeName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (pid.HasValue)
        {
            try
            {
                using var process = Process.GetProcessById(pid.Value);
                var waitTask = process.WaitForExitAsync(cancellationToken);
                var timeoutTask = Task.Delay(timeout, cancellationToken);
                var completed = await Task.WhenAny(waitTask, timeoutTask);

                if (completed != waitTask)
                {
                    throw new TimeoutException($"Process {pid.Value} did not exit within {timeout.TotalSeconds} seconds.");
                }

                return;
            }
            catch (ArgumentException)
            {
                return;
            }
        }

        var processName = Path.GetFileNameWithoutExtension(appExeName);
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Process.GetProcessesByName(processName).Length == 0)
            {
                return;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException($"Process '{processName}' did not exit within {timeout.TotalSeconds} seconds.");
    }

    public void StartApplication(string exePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }
}
