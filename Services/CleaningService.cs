// Services/CleaningService.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AutoQAC.Models;
using Mutagen.Bethesda.Environments;

namespace AutoQAC.Services;

/// <summary>
/// The CleaningService class provides functionality for managing the cleaning process
/// of plugins in a software application. It offers methods to detect the game mode,
/// retrieve the list of plugins to clean, and execute the cleaning process asynchronously.
/// </summary>
public class CleaningService(AutoQacConfiguration config, PluginInfo pluginInfo)
{
    private readonly ISubject<CleaningProgress> _progress = new Subject<CleaningProgress>();

    public IObservable<CleaningProgress> Progress => _progress;

    /// <summary>
    /// Cleans the specified plugins asynchronously. The cleaning process includes
    /// detecting the game mode from the load order file, retrieving the list of plugins to clean,
    /// and processing each plugin sequentially while reporting cleaning progress.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token used to monitor for cancellation requests while the cleaning process is in progress.
    /// </param>
    /// <returns>
    /// A Task representing the asynchronous cleaning operation.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the game mode cannot be detected from the load order path.
    /// </exception>
    public async Task CleanPluginsAsync(CancellationToken cancellationToken)
    {
        var gameMode = GameService.DetectGameMode(config.LoadOrderPath);
        if (gameMode == null)
            throw new InvalidOperationException("Unable to detect game mode from load order file");

        var plugins = await GetPluginsToCleanAsync();
        var totalPlugins = plugins.Count;

        _progress.OnNext(new CleaningProgress(0, totalPlugins, "Starting cleaning process..."));

        for (var i = 0; i < plugins.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var plugin = plugins[i];

            await CleanPluginAsync(plugin, i + 1, totalPlugins);
        }
    }

    /// <summary>
    /// Retrieves the list of plugins to be cleaned asynchronously. The method determines
    /// whether to use Mutagen's load order capabilities or fallback to text file parsing
    /// based on the game's characteristics and configuration.
    /// </summary>
    /// <returns>
    /// A Task representing the asynchronous operation, which upon completion returns
    /// a list of plugin filenames to be cleaned.
    /// </returns>
    private async Task<List<string>> GetPluginsToCleanAsync()
    {
        var gameMode = GameService.DetectGameMode(config.LoadOrderPath);

        // For Mutagen-supported games, use its load order capabilities
        if (GameService.IsMutagenSupported(gameMode))
        {
            var release = GameService.GetGameRelease(gameMode!);
            var env = GameEnvironment.Typical.Construct(release);
            var loadOrder = env.LoadOrder.ListedOrder.Where(x => !x.Ghosted)
                .Where(x => !GameService.IsEmptyPlugin(x.ModKey.FileName))
                .Where(x => !GameService.HasMissingMasters(x.ModKey.FileName, gameMode!)).ToString().Split(["\n", "\r\n"], StringSplitOptions.None)
                .ToList();
            return loadOrder;
        }

        // Fallback to text file parsing for non-Mutagen games
        var content = await File.ReadAllLinesAsync(config.LoadOrderPath);
        return content
            .Skip(1) // Skip first line
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Replace("*", "").Trim())
            .Where(line => !pluginInfo.LocalSkipList.Contains(line))
            .ToList();
    }

    /// <summary>
    /// Cleans a specific plugin asynchronously. The cleaning process involves executing the
    /// required tool with appropriate arguments, monitoring the cleaning outcome, and
    /// updating the cleaning progress.
    /// </summary>
    /// <param name="plugin">
    /// The name of the plugin to be cleaned.
    /// </param>
    /// <param name="current">
    /// The current index of the plugin in the cleaning sequence.
    /// </param>
    /// <param name="total">
    /// The total number of plugins in the cleaning sequence.
    /// </param>
    /// <returns>
    /// A Task representing the asynchronous cleaning operation.
    /// </returns>
    /// <exception cref="Exception">
    /// Thrown if the cleaning process encounters an error for the specified plugin.
    /// </exception>
    private async Task CleanPluginAsync(string plugin, int current, int total)
    {
        _progress.OnNext(new CleaningProgress(current, total, $"Cleaning {plugin}..."));
        var xEditArgs = BuildXEditArgs(plugin);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = config.XEditPath,
                Arguments = xEditArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        try
        {
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
                pluginInfo.PluginsCleaned++;
            else
                pluginInfo.CleanFailedList.Add(plugin);
        }
        catch (Exception ex)
        {
            pluginInfo.CleanFailedList.Add(plugin);
            throw new Exception($"Failed to clean plugin {plugin}: {ex.Message}");
        }

        pluginInfo.PluginsProcessed++;
    }

    /// <summary>
    /// Builds the arguments string required to run xEdit for cleaning a specific plugin.
    /// The arguments include predefined options for automation and may include additional flags
    /// based on the current configuration.
    /// </summary>
    /// <param name="plugin">
    /// The name of the plugin file to be cleaned.
    /// </param>
    /// <returns>
    /// A string containing the formatted xEdit arguments for the specified plugin.
    /// </returns>
    private string BuildXEditArgs(string plugin)
    {
        var args = $"-QAC -autoexit -autoload {plugin}";
        if (config.PartialForms)
            args = $"-iknowwhatimdoing {args} -allowmakepartial";
        return args;
    }
}