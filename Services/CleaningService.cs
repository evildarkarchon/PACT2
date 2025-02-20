using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AutoQAC.Models;

namespace AutoQAC.Services;

public class CleaningService
{
    private readonly AutoQacConfiguration _config;
    private readonly PluginInfo _pluginInfo;
    private readonly LoggingService _loggingService;
    private readonly XEditProcessService _xEditProcessService;
    private readonly ISubject<CleaningProgress> _progress;
    private readonly string _xEditLogPath;
    private readonly string _xEditExceptionLogPath;

    public IObservable<CleaningProgress> Progress => _progress;

    public CleaningService(
        AutoQacConfiguration config,
        PluginInfo pluginInfo,
        LoggingService loggingService,
        XEditProcessService xEditProcessService)
    {
        _config = config;
        _pluginInfo = pluginInfo;
        _loggingService = loggingService;
        _xEditProcessService = xEditProcessService;
        _progress = new Subject<CleaningProgress>();

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

    public async Task CleanPluginAsync(string pluginName, string gameMode, CancellationToken cancellationToken)
    {
        try
        {
            // Check if xEdit is already running
            if (await _xEditProcessService.IsXEditRunningAsync(gameMode))
            {
                throw new InvalidOperationException(
                    "xEdit is already running. Please close it before starting the cleaning process.");
            }

            // Clear existing logs before starting
            if (!_config.DebugMode)
            {
                await _loggingService.ClearXEditLogAsync(_xEditLogPath);
                await _loggingService.ClearXEditLogAsync(_xEditExceptionLogPath);
            }

            var xEditArgs = BuildXEditArgs(pluginName);
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

            var tcs = new TaskCompletionSource<bool>();
            process.Exited +=
                [SuppressMessage("ReSharper", "UnusedParameter.Local")](sender, args) => tcs.TrySetResult(true);
            process.EnableRaisingEvents = true;

            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_config.CleaningTimeout));

            try
            {
                await Task.WhenAny(tcs.Task, Task.Delay(-1, cts.Token));

                if (!process.HasExited)
                {
                    process.Kill();
                    throw new TimeoutException($"Cleaning timed out after {_config.CleaningTimeout} seconds");
                }
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }

                throw;
            }

            // Process cleaning results
            var cleaningFound = await _loggingService.ProcessXEditLogAsync(_xEditLogPath, pluginName);
            if (cleaningFound)
            {
                _pluginInfo.PluginsCleaned++;
                await _loggingService.LogToJournalAsync($"Successfully cleaned {pluginName}");
            }
            else
            {
                await _loggingService.LogToJournalAsync($"{pluginName} -> Nothing to clean, adding to ignore list");
            }
        }
        catch (Exception ex)
        {
            _pluginInfo.CleanFailedList.Add(pluginName);
            await _loggingService.LogToJournalAsync($"{pluginName} -> Cleaning failed: {ex.Message}");
            throw;
        }
        finally
        {
            // Ensure any remaining xEdit process is closed
            await _xEditProcessService.EnsureXEditClosedAsync(gameMode);
            _pluginInfo.PluginsProcessed++;
        }
    }

    private string BuildXEditArgs(string plugin)
    {
        var args = $"-QAC -autoexit -autoload \"{plugin}\"";
        if (_config.PartialForms)
        {
            args = $"-iknowwhatimdoing {args} -allowmakepartial";
        }

        return args;
    }
}