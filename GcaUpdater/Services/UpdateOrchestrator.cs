using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GcaUpdater.Services;

public sealed class UpdateOrchestrator
{
    private readonly UpdaterOptions _options;
    private readonly AppLogger _logger;
    private readonly IUpdateUi _ui;
    private readonly FileDeployService _fileDeployService = new();
    private readonly ProcessService _processService = new();

    public UpdateOrchestrator(UpdaterOptions options, AppLogger logger, IUpdateUi ui)
    {
        _options = options;
        _logger = logger;
        _ui = ui;
    }

    public async Task RunAsync()
    {
        using var cts = new CancellationTokenSource();

        try
        {
            LogAndShow("Preparing update workspace...", 2);
            _fileDeployService.PrepareDirectories(_options);

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var releaseService = new GitHubReleaseService(httpClient);

            LogAndShow("Checking latest GitHub release...", 8);
            var release = await releaseService.GetLatestReleaseAsync(_options.Owner, _options.Repo, cts.Token);
            _logger.Log($"Local version: {_options.CurrentVersion}");
            _logger.Log($"Remote version: {release.TagName}");

            if (!VersionHelper.IsRemoteNewer(_options.CurrentVersion, release.TagName))
            {
                LogAndShow("No update required. Restarting GcaEditor...", 100);
                await _processService.WaitForExitAsync(_options.ProcessIdToWait, _options.AppExeName, TimeSpan.FromSeconds(30), cts.Token);
                _processService.StartApplication(_options.AppExePath);
                _ui.EnableCloseButton();
                await Task.Delay(800, cts.Token);
                Application.Current.Shutdown(0);
                return;
            }

            var zipAsset = releaseService.PickZipAsset(release);
            _logger.Log($"Selected release asset: {zipAsset.Name}");
            _logger.Log($"Download URL: {zipAsset.BrowserDownloadUrl}");

            LogAndShow($"Downloading {zipAsset.Name}...", 14);
            var progress = new Progress<double>(value => _ui.SetProgress(14 + value * 0.36));
            await releaseService.DownloadFileAsync(zipAsset.BrowserDownloadUrl, _options.ZipPath, progress, cts.Token);

            LogAndShow("Waiting for GcaEditor to close...", 54);
            await _processService.WaitForExitAsync(_options.ProcessIdToWait, _options.AppExeName, TimeSpan.FromSeconds(30), cts.Token);

            LogAndShow("Extracting release package...", 62);
            var extractedRoot = _fileDeployService.ExtractRelease(_options.ZipPath, _options.ExtractDirectory);

            LogAndShow("Creating backup...", 70);
            _fileDeployService.BackupCurrentInstallation(_options.AppDirectory, _options.BackupDirectory);

            try
            {
                LogAndShow("Deploying new files...", 82);
                _fileDeployService.DeployRelease(extractedRoot, _options.AppDirectory);
            }
            catch (Exception deployEx)
            {
                _logger.Log("Deployment failed. Restoring backup...");
                _logger.Log(deployEx.ToString());
                _fileDeployService.RestoreBackup(_options.BackupDirectory, _options.AppDirectory);
                throw;
            }

            if (!File.Exists(_options.AppExePath))
            {
                throw new FileNotFoundException("Updated GcaEditor executable not found after deployment.", _options.AppExePath);
            }

            LogAndShow("Restarting GcaEditor...", 96);
            _processService.StartApplication(_options.AppExePath);

            LogAndShow("Update completed successfully.", 100);
            _ui.EnableCloseButton();
            await Task.Delay(1200, cts.Token);
            Application.Current.Shutdown(0);
        }
        catch (Exception ex)
        {
            _logger.Log(ex.ToString());
            _ui.SetProgress(0);
            _ui.SetStatus("Update failed.");
            _ui.AppendLog(ex.Message);
            _ui.ShowError($"The update could not be completed.{Environment.NewLine}{Environment.NewLine}{ex.Message}");
            _ui.EnableCloseButton();
        }
    }

    private void LogAndShow(string message, double progress)
    {
        _logger.Log(message);
        _ui.SetStatus(message);
        _ui.SetProgress(progress);
        _ui.AppendLog(message);
    }
}
