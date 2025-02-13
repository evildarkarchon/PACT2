// Services/LoggingService.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoQAC.Models;

namespace AutoQAC.Services;

/// <summary>
/// Provides logging functionality for the application, handling both journal logs
/// and xEdit log parsing.
/// </summary>
public partial class LoggingService(AutoQacConfiguration config, PluginInfo pluginInfo, string? journalPath = null)
{
    private readonly string _journalPath = journalPath ?? DefaultJournalPath;
    private static readonly string DefaultJournalPath = "AutoQAC Journal.log";

    // Regular expressions for parsing xEdit logs
    [GeneratedRegex(@"Undeleting:\s*(.*)")]
    private static partial Regex UdrPattern();

    [GeneratedRegex(@"Removing:\s*(.*)")]
    private static partial Regex ItmPattern();

    [GeneratedRegex(@"Skipping:\s*(.*)")]
    private static partial Regex NvmPattern();

    [GeneratedRegex(@"Making Partial Form:\s*(.*)")]
    private static partial Regex PartialFormPattern();

    /// <summary>
    /// Appends a message to the journal log file.
    /// </summary>
    /// <param name="message">The message to log</param>
    public async Task LogToJournalAsync(string message)
    {
        if (!config.StatLogging) return;

        try
        {
            await File.AppendAllTextAsync(_journalPath, $"{message}\n");
        }
        catch (Exception ex)
        {
            // If we can't log to the journal, we'll need to surface this to the UI
            throw new InvalidOperationException($"Failed to write to journal: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if the journal file should be expired based on configuration settings.
    /// </summary>
    public async Task CheckJournalExpirationAsync()
    {
        if (!File.Exists(_journalPath)) return;

        var fileInfo = new FileInfo(_journalPath);
        var age = DateTime.Now - fileInfo.LastWriteTime;

        if (age.Days > config.JournalExpiration)
        {
            try
            {
                File.Delete(_journalPath);
                await LogToJournalAsync($"Journal expired after {config.JournalExpiration} days and was cleared.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to expire journal: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Processes an xEdit log file and updates cleaning statistics.
    /// </summary>
    /// <param name="logPath">Path to the xEdit log file</param>
    /// <param name="pluginName">Name of the plugin being processed</param>
    /// <returns>True if any cleaning actions were found, false otherwise</returns>
    public async Task<bool> ProcessXEditLogAsync(string logPath, string pluginName)
    {
        if (!File.Exists(logPath)) return false;

        var cleaningFound = false;
        var logEntries = new List<string>();

        try
        {
            var logContent = await File.ReadAllLinesAsync(logPath);

            foreach (var line in logContent)
            {
                if (!ProcessLogLine(line, pluginName, out var logEntry)) continue;
                cleaningFound = true;
                if (logEntry != null)
                {
                    logEntries.Add(logEntry);
                }
            }

            // Log all entries at once
            if (logEntries.Count > 0)
            {
                await LogToJournalAsync($"\n{pluginName} ->");
                foreach (var entry in logEntries)
                {
                    await LogToJournalAsync(entry);
                }
            }

            if (!cleaningFound)
            {
                await LogToJournalAsync($"\n{pluginName} -> NOTHING TO CLEAN");
            }

            return cleaningFound;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to process xEdit log for {pluginName}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Processes a single line from the xEdit log file.
    /// </summary>
    /// <param name="line">The log line to process</param>
    /// <param name="pluginName">Name of the plugin being processed</param>
    /// <param name="logEntry">The generated log entry if any</param>
    /// <returns>True if a cleaning action was found, false otherwise</returns>
    private bool ProcessLogLine(string line, string pluginName, out string? logEntry)
    {
        logEntry = null;

        // Check each pattern and update the corresponding set
        if (UdrPattern().Match(line) is { Success: true } udrMatch)
        {
            pluginInfo.CleanResultsUdr.Add(pluginName);
            logEntry = "Cleaned UDRs";
            return true;
        }

        if (ItmPattern().Match(line) is { Success: true } itmMatch)
        {
            pluginInfo.CleanResultsItm.Add(pluginName);
            logEntry = "Cleaned ITMs";
            return true;
        }

        if (NvmPattern().Match(line) is { Success: true } nvmMatch)
        {
            pluginInfo.CleanResultsNvm.Add(pluginName);
            logEntry = "Found Deleted Navmeshes";
            return true;
        }

        if (PartialFormPattern().Match(line) is not { Success: true } partialMatch) return false;
        pluginInfo.CleanResultsPartialForms.Add(pluginName);
        logEntry = "Created Partial Forms";
        return true;

    }

    /// <summary>
    /// Clears the contents of an xEdit log file.
    /// </summary>
    /// <param name="logPath">Path to the log file to clear</param>
    public async Task ClearXEditLogAsync(string logPath)
    {
        if (!config.DebugMode && File.Exists(logPath))
        {
            try
            {
                await File.WriteAllTextAsync(logPath, string.Empty);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to clear xEdit log: {ex.Message}", ex);
            }
        }
    }
}