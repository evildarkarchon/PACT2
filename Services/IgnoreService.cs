// Services/IgnoreService.cs
using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AutoQAC.Services;

/// <summary>
/// Provides functionality to manage and modify ignore lists for plugins
/// associated with specific game modes.
/// </summary>
public class IgnoreService
{
    private readonly string _ignorePath;
    private readonly string _defaultIgnorePath;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    private static readonly string DefaultIgnoreContent = @"
auto_qac_ignore:
  sse: []
  fo4: []
  fo3: []
  fnv: []
  tes4: []
";

    /// <summary>
    /// A service that provides methods for managing and modifying ignore lists
    /// for plugins associated with specific game modes.
    /// </summary>
    public IgnoreService(
        string ignorePath = "AutoQAC Ignore.yaml",
        string defaultIgnorePath = "Data/Default Ignore.yaml")
    {
        _ignorePath = ignorePath;
        _defaultIgnorePath = defaultIgnorePath;

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    /// Retrieves the ignore list for the specified game mode.
    /// </summary>
    /// <param name="gameMode">The game mode for which the ignore list is to be retrieved.</param>
    /// <returns>A list of strings representing the ignore list for the specified game mode.</returns>
    public List<string> GetIgnoreList(string gameMode)
    {
        try
        {
            EnsureIgnoreFile();
            var yaml = File.ReadAllText(_ignorePath);
            var ignoreData = _deserializer.Deserialize<IgnoreData>(yaml);
            return ignoreData.GetGameList(gameMode);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get ignore list for {gameMode}", ex);
        }
    }

    /// <summary>
    /// Adds a plugin to the ignore list for a specific game mode.
    /// </summary>
    /// <param name="plugin">The name of the plugin to be added to the ignore list.</param>
    /// <param name="gameMode">The game mode for which the plugin should be ignored.</param>
    public void AddToIgnoreList(string plugin, string gameMode)
    {
        try
        {
            EnsureIgnoreFile();
            var yaml = File.ReadAllText(_ignorePath);
            var ignoreData = _deserializer.Deserialize<IgnoreData>(yaml);
            
            var gameList = ignoreData.GetGameList(gameMode);
            if (!gameList.Contains(plugin))
            {
                gameList.Add(plugin);
                var updatedYaml = _serializer.Serialize(ignoreData);
                File.WriteAllText(_ignorePath, updatedYaml);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to add {plugin} to ignore list for {gameMode}", ex);
        }
    }

    /// <summary>
    /// Ensures that the ignore file exists at the specified path.
    /// If the file does not exist, it will either copy a default ignore file to the path
    /// or create a new file with default content.
    /// </summary>
    private void EnsureIgnoreFile()
    {
        if (!File.Exists(_ignorePath))
        {
            if (File.Exists(_defaultIgnorePath))
            {
                File.Copy(_defaultIgnorePath, _ignorePath);
            }
            else
            {
                File.WriteAllText(_ignorePath, DefaultIgnoreContent);
            }
        }
    }

    /// <summary>
    /// Represents the data structure for ignore lists categorized by game modes.
    /// </summary>
    private class IgnoreData
    {
        /// <summary>
        /// Represents a section of ignore data specific to various game modes,
        /// containing lists of entries categorized by game mode.
        /// </summary>
        public class AutoQacIgnoreSection
        {
            public List<string> Sse { get; set; } = new();
            public List<string> Fo4 { get; set; } = new();
            public List<string> Fo3 { get; set; } = new();
            public List<string> Fnv { get; set; } = new();
            public List<string> Tes4 { get; set; } = new();
        }

        /// <summary>
        /// Represents the ignore data section that contains categorized ignore lists
        /// for different game modes such as SSE, FO4, FO3, FNV, and TES4.
        /// </summary>
        public AutoQacIgnoreSection AutoQacIgnore { get; set; } = new();

        /// <summary>
        /// Retrieves a list of ignored plugins for a specific game mode.
        /// </summary>
        /// <param name="gameMode">The game mode for which the ignore list is being retrieved.</param>
        /// <returns>A list of plugins ignored in the specified game mode.</returns>
        public List<string> GetGameList(string gameMode) => gameMode.ToLowerInvariant() switch
        {
            "sse" => AutoQacIgnore.Sse,
            "fo4" => AutoQacIgnore.Fo4,
            "fo3" => AutoQacIgnore.Fo3,
            "fnv" => AutoQacIgnore.Fnv,
            "tes4" => AutoQacIgnore.Tes4,
            _ => throw new ArgumentException($"Unknown game mode: {gameMode}", nameof(gameMode))
        };
    }
}