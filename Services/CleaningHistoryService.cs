using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoQAC.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AutoQAC.Services;

public class CleaningHistoryService
{
    private readonly string _historyPath;
    private readonly string _defaultHistoryPath;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;
    private readonly LoggingService _loggingService;

    public CleaningHistoryService(
        LoggingService loggingService,
        string historyPath = "AutoQAC History.yaml",
        string defaultHistoryPath = "Data/Default History.yaml")
    {
        _historyPath = historyPath;
        _defaultHistoryPath = defaultHistoryPath;
        _loggingService = loggingService;

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    public async Task<List<CleaningRecord>> GetCleaningHistoryAsync(string gameMode)
    {
        try
        {
            await EnsureHistoryFileAsync();
            var yaml = await File.ReadAllTextAsync(_historyPath);
            var historyData = _deserializer.Deserialize<HistoryData>(yaml);
            var records = historyData.GetGameHistory(gameMode);

            return records.Select(r => new CleaningRecord
            {
                PluginName = r.Plugin,
                LastCleaned = r.Timestamp,
                Results = new CleaningResults
                {
                    HasUdr = r.Results.HasUdr,
                    HasItm = r.Results.HasItm,
                    HasNvm = r.Results.HasNvm,
                    HasPartialForms = r.Results.HasPartialForms,
                    AdditionalNotes = r.Results.AdditionalNotes
                }
            }).ToList();
        }
        catch (Exception ex)
        {
            await _loggingService.LogToJournalAsync($"Failed to get cleaning history for {gameMode}: {ex.Message}");
            return new List<CleaningRecord>();
        }
    }

    public async Task AddToHistoryAsync(string pluginName, string gameMode, CleaningResults results)
    {
        try
        {
            await EnsureHistoryFileAsync();
            var yaml = await File.ReadAllTextAsync(_historyPath);
            var historyData = _deserializer.Deserialize<HistoryData>(yaml);
            
            var record = new PluginHistoryEntry
            {
                Plugin = pluginName,
                Timestamp = DateTime.UtcNow,
                Results = results
            };

            historyData.AddRecord(gameMode, record);
            
            var updatedYaml = _serializer.Serialize(historyData);
            await File.WriteAllTextAsync(_historyPath, updatedYaml);

            await _loggingService.LogToJournalAsync($"Added cleaning record for {pluginName} to {gameMode} history");
        }
        catch (Exception ex)
        {
            await _loggingService.LogToJournalAsync($"Failed to add {pluginName} to cleaning history for {gameMode}: {ex.Message}");
            throw;
        }
    }

    private async Task EnsureHistoryFileAsync()
    {
        if (!File.Exists(_historyPath))
        {
            if (!File.Exists(_defaultHistoryPath))
            {
                throw new FileNotFoundException("Default history file not found", _defaultHistoryPath);
            }
            File.Copy(_defaultHistoryPath, _historyPath);
            await _loggingService.LogToJournalAsync("Created new history file from default template");
        }
    }

    private class HistoryData
    {
        public class AutoQacHistorySection
        {
            public List<PluginHistoryEntry> Sse { get; set; } = new();
            public List<PluginHistoryEntry> Fo4 { get; set; } = new();
            public List<PluginHistoryEntry> Fo3 { get; set; } = new();
            public List<PluginHistoryEntry> Fnv { get; set; } = new();
            public List<PluginHistoryEntry> Tes4 { get; set; } = new();
        }

        public AutoQacHistorySection AutoQacHistory { get; set; } = new();

        public List<PluginHistoryEntry> GetGameHistory(string gameMode) => gameMode.ToLowerInvariant() switch
        {
            "sse" => AutoQacHistory.Sse,
            "fo4" => AutoQacHistory.Fo4,
            "fo3" => AutoQacHistory.Fo3,
            "fnv" => AutoQacHistory.Fnv,
            "tes4" => AutoQacHistory.Tes4,
            _ => throw new ArgumentException($"Unknown game mode: {gameMode}", nameof(gameMode))
        };

        public void AddRecord(string gameMode, PluginHistoryEntry record)
        {
            var history = GetGameHistory(gameMode);
            
            // Remove any existing record for this plugin
            history.RemoveAll(r => r.Plugin == record.Plugin);
            
            // Add the new record
            history.Add(record);
        }
    }

    private class PluginHistoryEntry
    {
        public required string Plugin { get; init; }
        public required DateTime Timestamp { get; init; }
        public required CleaningResults Results { get; init; }
    }
}