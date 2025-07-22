// Services/IgnoreService.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Mutagen.Bethesda;

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
        var combinedList = new List<string>();
        
        // Add hardcoded skip lists first
        var hardcodedList = GetHardcodedSkipList(gameMode);
        combinedList.AddRange(hardcodedList);
        
        try
        {
            EnsureIgnoreFile();
            var yaml = File.ReadAllText(_ignorePath);
            var ignoreData = _deserializer.Deserialize<IgnoreData>(yaml);
            var yamlList = ignoreData.GetGameList(gameMode);
            combinedList.AddRange(yamlList);
        }
        catch (Exception)
        {
            // Failed to load YAML ignore list, but we still have the hardcoded list
            // This ensures we always have at least the basic ignores (masters, DLCs, etc.)
        }
        
        // Remove duplicates and return
        return combinedList.Distinct().ToList();
    }

    /// <summary>
    /// Retrieves the ignore list for the specified GameRelease.
    /// </summary>
    /// <param name="gameRelease">The GameRelease for which the ignore list is to be retrieved.</param>
    /// <returns>A list of strings representing the ignore list for the specified GameRelease.</returns>
    public List<string> GetIgnoreListForGameRelease(GameRelease gameRelease)
    {
        var gameMode = ConvertGameReleaseToGameMode(gameRelease);
        return GetIgnoreList(gameMode);
    }

    /// <summary>
    /// Determines if a plugin should be ignored based on the ignore list for the specified game mode.
    /// Uses case-insensitive matching and supports partial matches for ignore entries without file extensions.
    /// </summary>
    /// <param name="pluginName">The name of the plugin to check.</param>
    /// <param name="gameMode">The game mode to check against.</param>
    /// <returns>True if the plugin should be ignored, false otherwise.</returns>
    public bool ShouldIgnorePlugin(string pluginName, string gameMode)
    {
        var ignoreList = GetIgnoreList(gameMode);
        return ShouldIgnorePlugin(pluginName, ignoreList);
    }

    /// <summary>
    /// Determines if a plugin should be ignored based on the ignore list for the specified GameRelease.
    /// Uses case-insensitive matching and supports partial matches for ignore entries without file extensions.
    /// </summary>
    /// <param name="pluginName">The name of the plugin to check.</param>
    /// <param name="gameRelease">The GameRelease to check against.</param>
    /// <returns>True if the plugin should be ignored, false otherwise.</returns>
    public bool ShouldIgnorePlugin(string pluginName, GameRelease gameRelease)
    {
        var gameMode = ConvertGameReleaseToGameMode(gameRelease);
        return ShouldIgnorePlugin(pluginName, gameMode);
    }

    /// <summary>
    /// Determines if a plugin should be ignored based on the provided ignore list.
    /// Uses case-insensitive matching and supports partial matches for ignore entries without file extensions.
    /// </summary>
    /// <param name="pluginName">The name of the plugin to check.</param>
    /// <param name="ignoreList">The list of ignore patterns to check against.</param>
    /// <returns>True if the plugin should be ignored, false otherwise.</returns>
    public bool ShouldIgnorePlugin(string pluginName, List<string> ignoreList)
    {
        if (string.IsNullOrWhiteSpace(pluginName) || ignoreList.Count == 0)
            return false;

        foreach (var ignoreEntry in ignoreList)
        {
            if (string.IsNullOrWhiteSpace(ignoreEntry))
                continue;

            // Check for exact case-insensitive match first
            if (string.Equals(pluginName, ignoreEntry, StringComparison.OrdinalIgnoreCase))
                return true;

            // If the ignore entry doesn't have an extension, try partial matching
            if (!HasFileExtension(ignoreEntry))
            {
                var pluginNameWithoutExt = Path.GetFileNameWithoutExtension(pluginName);
                if (string.Equals(pluginNameWithoutExt, ignoreEntry, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines if a string has a file extension.
    /// </summary>
    /// <param name="fileName">The filename to check.</param>
    /// <returns>True if the filename has an extension, false otherwise.</returns>
    private bool HasFileExtension(string fileName)
    {
        return !string.IsNullOrWhiteSpace(Path.GetExtension(fileName));
    }

    /// <summary>
    /// Converts a GameRelease to the corresponding game mode string.
    /// </summary>
    /// <param name="gameRelease">The GameRelease to convert.</param>
    /// <returns>The game mode string corresponding to the GameRelease.</returns>
    private static string ConvertGameReleaseToGameMode(GameRelease gameRelease)
    {
        return gameRelease switch
        {
            GameRelease.SkyrimSE => "sse",
            GameRelease.SkyrimSEGog => "sse",
            GameRelease.SkyrimVR => "sse",
            GameRelease.Fallout4 => "fo4",
            GameRelease.Fallout4VR => "fo4",
            GameRelease.Oblivion => "tes4",
            _ => throw new ArgumentException($"Unsupported GameRelease: {gameRelease}", nameof(gameRelease))
        };
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
            if (gameList.Contains(plugin)) return;
            gameList.Add(plugin);
            var updatedYaml = _serializer.Serialize(ignoreData);
            File.WriteAllText(_ignorePath, updatedYaml);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to add {plugin} to ignore list for {gameMode}", ex);
        }
    }

    /// <summary>
    /// Gets the hardcoded skip list for the specified game mode.
    /// </summary>
    /// <param name="gameMode">The game mode (fo3, fo4, sse, fnv, tes4).</param>
    /// <returns>A list of strings representing the hardcoded skip list for the game mode.</returns>
    private List<string> GetHardcodedSkipList(string gameMode)
    {
        return gameMode.ToLower() switch
        {
            "fo3" => Constants.SkipLists.Fo3.ToList(),
            "fo4" => Constants.SkipLists.Fo4.ToList(),
            "sse" => Constants.SkipLists.Skyrim.ToList(),
            "fnv" => Constants.SkipLists.Fnv.ToList(),
            "tes4" => Constants.SkipLists.Tes4.ToList(),
            _ => []
        };
    }

    /// <summary>
    /// Ensures that the ignore file exists at the specified path.
    /// If the file does not exist, it will either copy a default ignore file to the path
    /// or create a new file with default content.
    /// </summary>
    private void EnsureIgnoreFile()
    {
        if (File.Exists(_ignorePath)) return;
        if (File.Exists(_defaultIgnorePath))
        {
            File.Copy(_defaultIgnorePath, _ignorePath);
        }
    }

    /// <summary>
    /// Represents the data structure for ignore lists categorized by game modes.
    /// </summary>
    public class IgnoreData
    {
        /// <summary>
        /// Represents a section of ignore data specific to various game modes,
        /// containing lists of entries categorized by game mode.
        /// </summary>
        public class AutoQacIgnoreSection
        {
            public List<string> Sse { get; set; } = [];
            public List<string> Fo4 { get; set; } = [];
            public List<string> Fo3 { get; set; } = [];
            public List<string> Fnv { get; set; } = [];
            public List<string> Tes4 { get; set; } = [];
        }

        /// <summary>
        /// Represents the ignore data section that contains categorized ignore lists
        /// for different game modes such as SSE, FO4, FO3, FNV, and TES4.
        /// </summary>
        private AutoQacIgnoreSection AutoQacIgnore { get; set; } = new();

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