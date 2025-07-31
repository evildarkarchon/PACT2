using System;

namespace AutoQAC.Models;

public class CleaningRecord
{
    public required string PluginName { get; init; }
    public required DateTime LastCleaned { get; init; }
    public required CleaningResults Results { get; init; }
}

public class CleaningResults
{
    public bool HasUdr { get; init; }
    public bool HasItm { get; init; }
    public bool HasNvm { get; init; }
    public bool HasPartialForms { get; init; }
    public string? AdditionalNotes { get; init; }
}