using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GcaUpdater.Models;

namespace GcaUpdater.Services;

public sealed class GitHubReleaseService
{
    private readonly HttpClient _httpClient;

    public GitHubReleaseService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GcaUpdater", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken);

        return release ?? throw new InvalidOperationException("Unable to deserialize GitHub release response.");
    }

    public GitHubAsset PickZipAsset(GitHubRelease release)
    {
        if (release.Draft)
        {
            throw new InvalidOperationException("The latest GitHub release is still marked as draft.");
        }

        var zipAsset = release.Assets
            .Where(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.Size)
            .FirstOrDefault();

        return zipAsset ?? throw new InvalidOperationException("No .zip asset found in the latest GitHub release.");
    }

    public async Task DownloadFileAsync(string url, string destinationPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destinationPath);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;

            if (contentLength.HasValue && contentLength.Value > 0)
            {
                progress?.Report(totalRead * 100d / contentLength.Value);
            }
        }

        progress?.Report(100);
    }
}
