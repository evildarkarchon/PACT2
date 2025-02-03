// Services/UpdateService.cs
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AutoQAC.Models;

namespace AutoQAC.Services;

public class UpdateService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly LoggingService _loggingService;
    private bool _disposed;

    private class GitHubRelease
    {
        public string? Name { get; set; }
    }

    public UpdateService(LoggingService loggingService)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"AutoQAC/{VersionInfo.CurrentVersion}");
        _loggingService = loggingService;
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        try
        {
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(VersionInfo.UpdateCheckUrl);
            
            if (release?.Name == null)
            {
                await _loggingService.LogToJournalAsync("Update check failed: Unable to parse GitHub response");
                return new UpdateCheckResult(false, "Unable to parse GitHub response", null);
            }

            var isUpToDate = string.Equals(release.Name, VersionInfo.CurrentVersion, StringComparison.OrdinalIgnoreCase);

            if (!isUpToDate)
            {
                var message = $"A new version ({release.Name}) is available. Current version: {VersionInfo.CurrentVersion}";
                await _loggingService.LogToJournalAsync($"Update check: {message}");
                return new UpdateCheckResult(false, message, release.Name);
            }

            await _loggingService.LogToJournalAsync("Update check: You have the latest version");
            return new UpdateCheckResult(true, "You have the latest version", release.Name);
        }
        catch (Exception ex)
        {
            var message = $"Failed to check for updates: {ex.Message}";
            await _loggingService.LogToJournalAsync(message);
            return new UpdateCheckResult(false, message, null);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}

public record UpdateCheckResult(bool IsUpToDate, string Message, string? LatestVersion);