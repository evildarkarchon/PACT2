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
public class CleaningService
{
    public CleaningService(AutoQacConfiguration config, PluginInfo pluginInfo, LoggingService loggingService)
    {
        _config = config;
        _pluginInfo = pluginInfo;
        _loggingService = loggingService;

        // Set up xEdit log paths based on the executable path
        var xEditPath = new FileInfo(_config.XEditPath);
        _xEditLogPath = Path.Combine(
            xEditPath.DirectoryName!,
            $"{Path.GetFileNameWithoutExtension(xEditPath.Name).ToUpperInvariant()}_log.txt"
        );
        _xEditExceptionLogPath = Path.Combine(
            xEditPath.DirectoryName!,
            $"{Path.GetFileNameWithoutExtension(xEditPath.Name).ToUpperInvariant()}Exception.log"
        );
    }

    private readonly ISubject<CleaningProgress> _progress = new Subject<CleaningProgress>();
    private readonly AutoQacConfiguration _config;
    private readonly PluginInfo _pluginInfo;
    private readonly LoggingService _loggingService;
    private readonly string _xEditLogPath;
    private readonly string _xEditExceptionLogPath;

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
        // Check journal expiration before starting
        await _loggingService.CheckJournalExpirationAsync();

        var gameMode = GameService.DetectGameMode(_config.LoadOrderPath);
        if (gameMode == null)
            throw new InvalidOperationException("Unable to detect game mode from load order file");

        var plugins = await GetPluginsToCleanAsync();
        var totalPlugins = plugins.Count;

        _progress.OnNext(new CleaningProgress(0, totalPlugins, "Starting cleaning process..."));
        await _loggingService.LogToJournalAsync($"\nStarting cleaning process for {totalPlugins} plugins...");

        for (var i = 0; i < plugins.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await _loggingService.LogToJournalAsync("\nCleaning process cancelled by user");
                break;
            }

            var plugin = plugins[i];
            await CleanPluginAsync(plugin, i + 1, totalPlugins);
        }

        await _loggingService.LogToJournalAsync(
            $"\nCleaning process completed. Processed {_pluginInfo.PluginsProcessed} plugins.");
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
        var gameMode = GameService.DetectGameMode(_config.LoadOrderPath);

        // For Mutagen-supported games, use its load order capabilities
        if (GameService.IsMutagenSupported(gameMode))
        {
            var release = GameService.GetGameRelease(gameMode!);
            var env = GameEnvironment.Typical.Construct(release);
            var loadOrder = env.LoadOrder.ListedOrder.Where(x => !x.Ghosted)
                .Where(x => !GameService.IsEmptyPlugin(x.ModKey.FileName))
                .Where(x => !GameService.HasMissingMasters(x.ModKey.FileName, gameMode!)).ToString()!
                .Split(["\n", "\r\n"], StringSplitOptions.None)
                .ToList();
            return loadOrder;
        }

        // Fallback to text file parsing for non-Mutagen games
        var content = await File.ReadAllLinesAsync(_config.LoadOrderPath);
        return content
            .Skip(1) // Skip first line
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Replace("*", "").Trim())
            .Where(line => !_pluginInfo.LocalSkipList.Contains(line))
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

        try
        {
            // Clear existing logs before starting
            await _loggingService.ClearXEditLogAsync(_xEditLogPath);
            await _loggingService.ClearXEditLogAsync(_xEditExceptionLogPath);

            var xEditArgs = BuildXEditArgs(plugin);
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = _config.XEditPath,
                Arguments = xEditArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();
            await process.WaitForExitAsync();

            // Process cleaning results
            var cleaningFound = await _loggingService.ProcessXEditLogAsync(_xEditLogPath, plugin);
            if (cleaningFound)
            {
                _pluginInfo.PluginsCleaned++;
            }
            else
            {
                // If nothing was cleaned, add to ignore list
                await _loggingService.LogToJournalAsync($"{plugin} -> Nothing to clean, adding to ignore list");
            }
        }
        catch (Exception ex)
        {
            _pluginInfo.CleanFailedList.Add(plugin);
            await _loggingService.LogToJournalAsync($"{plugin} -> Cleaning failed: {ex.Message}");
            throw new Exception($"Failed to clean plugin {plugin}: {ex.Message}");
        }
        finally
        {
            _pluginInfo.PluginsProcessed++;
        }
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
        if (_config.PartialForms)
            args = $"-iknowwhatimdoing {args} -allowmakepartial";
        return args;
    }
}