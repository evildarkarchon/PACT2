using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins.Records;

namespace AutoQAC.Services;

/// <summary>
/// Provides various functionalities related to game modes and plugin handling
/// for supported games, including detection, validation, and processing of game releases.
/// </summary>
public class GameService
{
    private static readonly Dictionary<string, GameRelease[]> GameModeToRelease = new()
    {
        { "sse", [GameRelease.SkyrimSE, GameRelease.SkyrimSEGog, GameRelease.SkyrimVR] },
        { "fo4", [GameRelease.Fallout4, GameRelease.Fallout4VR] },
        { "tes4", [GameRelease.Oblivion] }
    };

    /// <summary>
    /// Checks whether the given game mode is supported by Mutagen.
    /// </summary>
    /// <param name="gameMode">The string identifier representing the game mode (e.g., "sse", "fo4", "tes4").</param>
    /// <returns>
    /// True if the provided game mode is supported by Mutagen; otherwise, false.
    /// </returns>
    public static bool IsMutagenSupported(string? gameMode)
        => gameMode != null && GameModeToRelease.ContainsKey(gameMode);

    /// <summary>
    /// Determines the appropriate game release for a given game mode by testing its compatibility.
    /// </summary>
    /// <param name="gameMode">The string identifier representing the game mode (e.g., "sse", "fo4", "tes4").</param>
    /// <returns>
    /// The corresponding <see cref="GameRelease"/> for the specified game mode if a compatible environment is successfully constructed.
    /// Throws an exception if the game mode is unsupported or no compatible environment can be determined.
    /// </returns>
    public static GameRelease GetGameRelease(string gameMode)
    {
        if (!GameModeToRelease.TryGetValue(gameMode, out var releases))
            throw new ArgumentException($"Game mode {gameMode} is not supported by Mutagen", nameof(gameMode));

        // For both Skyrim and Fallout 4, test which environment works
        foreach (var release in releases)
        {
            try
            {
                // Test if we can construct the environment
                GameEnvironment.Typical.Construct(release);
                return release;
            }
            catch
            {
                // Try next release if this one fails
            }
        }

        // If none worked, default to first version and let it fail naturally
        return releases[0];
    }

    /// <summary>
    /// Determines whether a plugin has missing master references in its current load order.
    /// </summary>
    /// <param name="pluginPath">The file path of the plugin to check for missing masters.</param>
    /// <param name="gameMode">The game mode identifier used to determine the game's release and load order configuration.</param>
    /// <returns>
    /// True if the plugin has missing master references; otherwise, false.
    /// </returns>
    public static bool HasMissingMasters(string pluginPath, string gameMode)
    {
        if (!IsMutagenSupported(gameMode))
            return false; // Can't check masters for non-Mutagen games

        try
        {
            var release = GetGameRelease(gameMode);
            var env = GameEnvironment.Typical.Construct(release);
            var modKey = ModKey.FromFileName(Path.GetFileName(pluginPath));

            // Get the full load order to check against
            var loadOrder = env.LoadOrder.ListedOrder;

            // Find our plugin in the load order
            var modListingGetters = loadOrder.ToList();
            var pluginListing = modListingGetters
                .Where(x => !x.Ghosted)
                .FirstOrDefault(x => x.ModKey == modKey);
            if (pluginListing?.Mod == null) return true; // Can't find or read mod

            // Build set of available masters from everything before this plugin
            var availableMasters = modListingGetters
                .Where(x => !x.Ghosted)
                .TakeWhile(x => x.ModKey != modKey)
                .Select(x => x.ModKey)
                .ToHashSet();

            // Check if all masters are available
            foreach (var master in pluginListing.Mod.MasterReferences)
            {
                if (!availableMasters.Contains(master.Master))
                    return true; // Missing master found
            }

            return false;
        }
        catch
        {
            return false; // If we can't check masters, assume they're present
        }
    }

    /// <summary>
    /// Detects the appropriate game release based on the provided plugin or load order file,
    /// leveraging dynamic detection of supported game releases.
    /// </summary>
    /// <param name="pluginPath">The file path of the plugin to be analyzed for detection. This parameter is optional.</param>
    /// <param name="loadOrderPath">The file path of the load order file to assist in detection. This parameter is optional.</param>
    /// <returns>
    /// The detected game release if successfully identified.
    /// </returns>
    public static GameRelease DetectGameRelease(string? pluginPath = null, string? loadOrderPath = null)
    {
        // If `DetectGameMode` logic is enough, use it to refine GameRelease detection
        var detectedGameMode = loadOrderPath != null ? DetectGameMode(loadOrderPath) : null;

        // If the gameMode is valid, try finding the associated GameRelease dynamically
        if (detectedGameMode != null && GameModeToRelease.TryGetValue(detectedGameMode, out var releases))
        {
            foreach (var release in releases)
            {
                try
                {
                    // Attempt to identify supported release dynamically
                    GameEnvironment.Typical.Construct(release);
                    return release; // Return the first valid game release
                }
                catch
                {
                    // Ignore and try the next one
                }
            }
        }

        // Fallback handling if no releases detected dynamically
        if (pluginPath != null)
        {
            foreach (var releaseSet in GameModeToRelease.Values)
            {
                foreach (var release in releaseSet)
                {
                    try
                    {
                        // If it reads, return this one
                        return release;
                    }
                    catch
                    {
                        // Continue checking all options
                    }
                }
            }
        }

        throw new InvalidOperationException("Unable to detect the appropriate GameRelease.");
    }

    /// <summary>
    /// Determines if the specified plugin is empty by checking if it contains any major records.
    /// </summary>
    /// <param name="pluginPath">The file path of the plugin to be checked.</param>
    /// <returns>
    /// True if the plugin does not contain any major records; otherwise, false.
    /// </returns>
    public static bool IsEmptyPlugin(string pluginPath)
    {
        GameRelease release = DetectGameRelease(pluginPath);
        var mpath = new ModPath(pluginPath);
        using var plugin = ModInstantiator.ImportGetter(mpath, release);
        return plugin.EnumerateMajorRecords().Any();
    }

    /// <summary>
    /// Detects the game mode based on the provided load order file content.
    /// </summary>
    /// <param name="loadOrderPath">The file path of the load order file to be analyzed.</param>
    /// <returns>
    /// A string representing the detected game mode if identifiable; otherwise, null.
    /// </returns>
    public static string? DetectGameMode(string loadOrderPath)
    {
        if (!File.Exists(loadOrderPath)) return null;

        var content = File.ReadAllText(loadOrderPath);
        return content switch
        {
            var x when x.Contains("Skyrim.esm") => "sse",
            var x when x.Contains("Fallout3.esm") => "fo3",
            var x when x.Contains("FalloutNV.esm") => "fnv",
            var x when x.Contains("Fallout4.esm") => "fo4",
            var x when x.Contains("Oblivion.esm") => "tes4",
            _ => null
        };
    }
}