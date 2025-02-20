using System;
using System.Collections.Generic;
using Noggog;

namespace AutoQAC.Models;

public class Plugin
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public bool IsEnabled { get; set; } = true;
    public string? Description { get; init; }

    // Cleaning history properties
    public DateTime? LastCleaned { get; init; }
    public CleaningResults? LastCleaningResults { get; init; }

    public string StatusDescription => GetStatusDescription();

    private string GetStatusDescription()
    {
        if (LastCleaned == null)
            return "Never cleaned";

        var timeAgo = DateTime.UtcNow - LastCleaned.Value;
        var timeDescription = timeAgo.TotalDays switch
        {
            < 1 => "today",
            < 2 => "yesterday",
            < 7 => $"{timeAgo.Days} days ago",
            < 30 => $"{timeAgo.Days / 7} weeks ago",
            < 365 => $"{timeAgo.Days / 30} months ago",
            _ => $"{timeAgo.Days / 365:F1} years ago"
        };

        var results = LastCleaningResults;
        if (results == null)
            return $"Last cleaned {timeDescription}";

        var cleaningDetails = new List<string>();
        if (results.HasUdr) cleaningDetails.Add("UDR");
        if (results.HasItm) cleaningDetails.Add("ITM");
        if (results.HasNvm) cleaningDetails.Add("NVM");
        if (results.HasPartialForms) cleaningDetails.Add("Partial Forms");

        return cleaningDetails.Any()
            ? $"Last cleaned {timeDescription} ({string.Join(", ", cleaningDetails)})"
            : $"Last cleaned {timeDescription} (No issues found)";
    }
}