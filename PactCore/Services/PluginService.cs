using System.Diagnostics;
using System.Text.RegularExpressions;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using PACT2.PactCore.Models;
using PACT2.PactCore.Services.Interfaces;

namespace PACT2.PactCore.Services;

public class PluginService(IDialogService dialogService, ILogService logService, ProgressEmitter progressEmitter)
{
    private static readonly Regex PluginRegex = new(@".+?\.(?:esl|esm|esp)+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task CleanPluginsAsync()
    {
        try 
        {
            await InitializeCleanProcessAsync();
            var (plugins, totalCount, skipList) = GetPluginInfo();
            var ignoreList = await GetIgnoreListAsync();

            Info.Instance.LocalSkipList.AddRange(ignoreList);
            
            progressEmitter.EmitMaxValue(totalCount);
            progressEmitter.SetVisibility();

            var startTime = DateTime.Now;
            var cleanedCount = await CleanPluginsAsync(plugins, skipList);

            await ReportCompletionAsync(startTime, cleanedCount, totalCount);
            await LogFailedPluginsAsync();
            progressEmitter.ReportDone();
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync("Plugin Cleaning Error", 
                $"An error occurred while cleaning plugins: {ex.Message}");
            progressEmitter.ReportDone();
        }
    }

    private async Task InitializeCleanProcessAsync()
    {
        progressEmitter.TaskCompleted = false;
        await logService.LogMessageAsync($"Load order TXT is set to: {Info.Instance.LoadOrderPath}");
        await logService.LogMessageAsync($"XEdit EXE is set to: {Info.Instance.XEditPath}");
    }

    private (List<string> plugins, int totalCount, List<string> skipList) GetPluginInfo()
    {
        var gameMode = GetGameMode();
        var plugins = GetPluginList(gameMode);
        var skipList = Info.Instance.VipSkipList.Concat(Info.Instance.LocalSkipList).ToList();
        var totalCount = plugins.Except(skipList, StringComparer.OrdinalIgnoreCase).Count();
        
        return (plugins, totalCount, skipList);
    }

    private async Task<List<string>> GetIgnoreListAsync()
    {
        var yamlSettings = YamlSettingsService.Instance;
        return await Task.Run(() => 
            yamlSettings.GetSetting<List<string>>("PACT Ignore.yaml", 
                $"PACT_Ignore_{GetGameMode().ToUpper()}") ?? new List<string>());
    }

    private List<string> GetPluginList(string gameMode)
    {
        return gameMode switch
        {
            "sse" => GetMutagenPlugins(GameRelease.SkyrimSE),
            "fo4" => GetMutagenPlugins(GameRelease.Fallout4),
            "fo3" or "fnv" => GetLegacyPlugins(),
            _ => throw new InvalidOperationException($"Unsupported game mode: {gameMode}")
        };
    }

    private string GetGamePath()
    {
        var loadOrderPath = Info.Instance.LoadOrderPath;
        if (loadOrderPath == null || string.IsNullOrWhiteSpace(loadOrderPath))
            throw new InvalidOperationException("LoadOrderPath is invalid.");
        if (!Path.IsPathFullyQualified(loadOrderPath))
            throw new InvalidOperationException("LoadOrderPath is not a fully qualified path.");

        var firstDir = Path.GetDirectoryName(loadOrderPath);
        if (firstDir == null)
            throw new InvalidOperationException("First-level directory could not be determined.");

        var gamePath = Path.GetDirectoryName(firstDir);
        if (gamePath == null)
            throw new InvalidOperationException("Could not determine game path from LoadOrderPath.");
        return gamePath;
    }

    private IGameEnvironment CreateEnvironment(GameRelease game)
    {
        // Get the game's root path
        var gamePath = GetGamePath();
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
            throw new InvalidOperationException($"Game path is invalid: {gamePath}");

        // Set the working directory to the game path
        Directory.SetCurrentDirectory(gamePath);

        // Construct and return the environment
        var env = GameEnvironment.Typical.Construct(game);
        if (env == null)
            throw new InvalidOperationException("Failed to construct the game environment.");

        return env;
    }

    private List<string> GetMutagenPlugins(GameRelease game)
    {
        // Create and validate the Mutagen environment
        var env = CreateEnvironment(game);

        // Ensure the LoadOrder is valid and process plugin file names
        if (env.LoadOrder.ListedOrder == null)
            throw new InvalidOperationException("No listed order available in game environment.");

        return env.LoadOrder.ListedOrder
            .Select(x => x.ModKey.FileName.ToString())
            .ToList();
    }

    private List<string> GetLegacyPlugins()
    {
        var content = File.ReadAllLines(Info.Instance.LoadOrderPath!);
        return content
            .Skip(1) // Skip first line
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => 
                Path.GetFileName(Info.Instance.LoadOrderPath)!.Equals("plugins.txt", StringComparison.OrdinalIgnoreCase)
                    ? line.Contains('*')
                    : !line.Contains(".ghost", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Trim().Replace("*", ""))
            .ToList();
    }

    private async Task<int> CleanPluginsAsync(List<string> plugins, List<string> skipList)
    {
        var cleanedCount = 0;
        foreach (var plugin in plugins)
        {
            if (ShouldClean(plugin, skipList))
            {
                progressEmitter.ReportPlugin(plugin);
                await CleanPluginAsync(plugin);
                cleanedCount++;
                await logService.LogMessageAsync($"Finished cleaning: {plugin} ({cleanedCount})");
                progressEmitter.ReportProgress(cleanedCount);
            }
        }
        return cleanedCount;
    }

    private bool ShouldClean(string plugin, List<string> skipList)
    {
        return !skipList.Any(skip => plugin.Contains(skip, StringComparison.OrdinalIgnoreCase)) 
            && PluginRegex.IsMatch(plugin);
    }

    private async Task CleanPluginAsync(string plugin)
    {
        await RunAutoCleaningAsync(plugin);
        await CheckCleaningResultsAsync(plugin);
    }

    private async Task RunAutoCleaningAsync(string plugin)
    {
        var command = CreateXEditCommand(plugin);
        if (string.IsNullOrEmpty(command))
        {
            throw new InvalidOperationException("Failed to create xEdit command");
        }

        await ClearXEditLogsAsync();
        await logService.LogMessageAsync($"Currently cleaning: {plugin}");

        // ReSharper disable once UsingStatementResourceInitialization
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Info.Instance.XEditPath,
                Arguments = command,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized
            }
        };

        try
        {
            process.Start();

            // Pass relevant information (e.g., process ID) to the monitor task instead
            var monitorTask = MonitorProcessAsync(process, plugin);

            await process.WaitForExitAsync();
            await monitorTask;

            Info.Instance.PluginsProcessed++;
        }
        finally
        {
            process.Dispose();
        }
    }

    private string? CreateXEditCommand(string plugin)
    {
        var xeditExe = Info.Instance.XEditExe?.ToLower();
        if (string.IsNullOrEmpty(xeditExe)) return null;

        if (Info.Instance.LowerSpecific.Contains(xeditExe))
        {
            UpdateLogPaths();
            return CreateSpecificCommand(plugin);
        }

        if (Info.Instance.LoadOrderPath?.Contains("loadorder", StringComparison.OrdinalIgnoreCase) == true 
            && Info.Instance.LowerUniversal.Contains(xeditExe))
        {
            var gameMode = GetGameMode();
            if (string.IsNullOrEmpty(gameMode))
            {
                dialogService.ShowErrorAsync("Invalid Configuration", "Unable to determine game mode from load order file.");
                return null;
            }

            UpdateLogPaths(gameMode);
            return CreateUniversalCommand(plugin, gameMode);
        }

        return null;
    }

    private string CreateSpecificCommand(string plugin)
    {
        return ModifyCommandForPartialForms($"-QAC -autoexit -autoload \"{plugin}\"");
    }

    private string CreateUniversalCommand(string plugin, string gameMode)
    {
        return ModifyCommandForPartialForms($"-{gameMode} -QAC -autoexit -autoload \"{plugin}\"");
    }

    private string ModifyCommandForPartialForms(string command)
    {
        var settings = YamlSettingsService.Instance;
        var allowPartialForms = settings.GetSetting<bool>("PACT Settings.yaml", "PACT_Settings.Partial Forms");
        
        return allowPartialForms 
            ? command.Replace("-QAC", "-iknowwhatimdoing -QAC -allowmakepartial") 
            : command;
    }

    private void UpdateLogPaths(string? gameMode = null)
    {
        if (string.IsNullOrEmpty(Info.Instance.XEditPath)) return;

        var path = Path.GetFileNameWithoutExtension(Info.Instance.XEditPath);
        var directory = Path.GetDirectoryName(Info.Instance.XEditPath) ?? "";

        var prefix = gameMode != null ? $"{gameMode.ToUpper()}Edit" : path.ToUpper();
        
        Info.Instance.XEditLogTxt = Path.Combine(directory, $"{prefix}_log.txt");
        Info.Instance.XEditExcLog = Path.Combine(directory, $"{prefix}Exception.log");
    }

    private async Task MonitorProcessAsync(Process process, string pluginName)
    {
        while (!process.HasExited)
        {
            var processes = Process.GetProcesses()
                .Where(p => MatchesCondition(p.ProcessName))
                .Where(p => p.ProcessName.Equals(Info.Instance.XEditExe, StringComparison.OrdinalIgnoreCase));

            foreach (var xeditProcess in processes)
            {
                if (await CheckProcessErrorsAsync(xeditProcess, pluginName))
                {
                    try 
                    {
                        process.Kill();
                        await process.WaitForExitAsync();
                    }
                    catch (Exception) { /* Process may have already exited */ }
                    return;
                }
            }

            await Task.Delay(3000);
        }
    }

    private bool MatchesCondition(string processName)
    {
        var normalizedName = Path.GetFileName(processName).ToLower();
        return Info.Instance.LowerSpecific.Contains(normalizedName) || 
               Info.Instance.LowerUniversal.Contains(normalizedName);
    }

    private async Task<bool> CheckProcessErrorsAsync(Process process, string pluginName)
    {
        if (await CheckCpuUsageAsync(process))
        {
            await HandleProcessErrorAsync(process, pluginName, "disabled_or_missing");
            return true;
        }

        if (CheckProcessTimeout(process))
        {
            await HandleProcessErrorAsync(process, pluginName, "timeout", false);
            return true;
        }

        if (await CheckProcessExceptionsAsync())
        {
            await HandleProcessErrorAsync(process, pluginName, "empty_or_missing");
            return true;
        }

        return false;
    }

    private async Task HandleProcessErrorAsync(Process process, string pluginName, string errorType, bool addToIgnore = true)
    {
        try
        {
            process.Kill();
        }
        catch (Exception) { /* Process may have already exited */ }

        await Task.Delay(1000);

        var settings = YamlSettingsService.Instance;
        if (!settings.GetSetting<bool>("PACT Settings.yaml", "PACT_Settings.Debug Mode"))
        {
            await ClearXEditLogsAsync();
        }

        Info.Instance.PluginsProcessed--;
        Info.Instance.CleanFailedList.Add(pluginName);

        var errorMessage = GetErrorMessage(errorType);
        await dialogService.ShowWarningAsync("Plugin Processing Error", errorMessage);

        if (addToIgnore)
        {
            await AddToIgnoreListAsync(pluginName, GetGameMode());
        }
    }

    private string GetErrorMessage(string errorType) => errorType switch
    {
        "disabled_or_missing" => "Plugin is disabled or has missing requirements.",
        "timeout" => "xEdit timed out (cleaning process took too long).",
        "empty_or_missing" => "Plugin is empty or has missing requirements.",
        _ => "An unknown error occurred while processing the plugin."
    };

    private async Task<bool> CheckCpuUsageAsync(Process process)
    {
        try
        {
            return process.HasExited || await Task.Run(() =>
            {
                process.Refresh();
                return process.TotalProcessorTime.TotalMilliseconds < 1000 ||
                       process.HasExited ||
                       process.Responding == false;
            });
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool CheckProcessTimeout(Process process)
    {
        try
        {
            return (DateTime.Now - process.StartTime).TotalSeconds > Info.Instance.CleaningTimeout;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> CheckProcessExceptionsAsync()
    {
        if (!File.Exists(Info.Instance.XEditExcLog)) return false;

        var content = await File.ReadAllTextAsync(Info.Instance.XEditExcLog);
        return content.Contains("which can not be found") || 
               content.Contains("which it does not have");
    }

    private bool CheckMutagenCompatible(string gameMode)
    {
        string[] compatibleModes = ["sse", "fo4"];
        return compatibleModes.Contains(gameMode);
    }
    
    private string GetGameMode()
    {
        if (string.IsNullOrEmpty(Info.Instance.LoadOrderPath)) 
            throw new InvalidOperationException("Load order path is not set");

        var content = File.ReadAllText(Info.Instance.LoadOrderPath);
        
        return content switch
        {
            var x when x.Contains("Skyrim.esm") => "sse",
            var x when x.Contains("Fallout3.esm") => "fo3",
            var x when x.Contains("FalloutNV.esm") => "fnv",
            var x when x.Contains("Fallout4.esm") => "fo4",
            _ => throw new InvalidOperationException("Unable to determine game mode")
        };
    }

    private async Task ClearXEditLogsAsync()
    {
        try
        {
            if (File.Exists(Info.Instance.XEditLogTxt))
                File.Delete(Info.Instance.XEditLogTxt);
            if (File.Exists(Info.Instance.XEditExcLog))
                File.Delete(Info.Instance.XEditExcLog);
        }
        catch (Exception)
        {
            await dialogService.ShowErrorAsync("Log Clearing Error", 
                "Unable to clear xEdit logs. Try running the application with administrator privileges.");
            throw;
        }
    }

    private Task AddToIgnoreListAsync(string plugin, string gameMode)
    {
        var yamlSettings = YamlSettingsService.Instance;
        var ignoreList = yamlSettings.GetSetting<List<string>>("PACT Ignore.yaml", 
            $"PACT_Ignore_{gameMode.ToUpper()}") ?? new List<string>();
        
        if (!ignoreList.Contains(plugin))
        {
            ignoreList.Add(plugin);
            yamlSettings.SetSetting("PACT Ignore.yaml", $"PACT_Ignore_{gameMode.ToUpper()}", ignoreList);
        }

        return Task.CompletedTask;
    }

    private async Task CheckCleaningResultsAsync(string plugin)
    {
        await Task.Delay(1000); // Ensure xEdit logs are generated
        
        if (!File.Exists(Info.Instance.XEditLogTxt))
            return;

        var didClean = false;
        var content = await File.ReadAllLinesAsync(Info.Instance.XEditLogTxt);
        
        foreach (var line in content)
        {
            if (await ProcessLogLineAsync(line, plugin))
                didClean = true;
        }

        if (didClean)
        {
            Info.Instance.PluginsCleaned++;
        }
        else
        {
            await logService.LogToJournalAsync($"\n{plugin} -> NOTHING TO CLEAN");
            await logService.LogMessageAsync("Nothing to clean! Adding plugin to PACT Ignore file...");
            await AddToIgnoreListAsync(plugin, GetGameMode());
            Info.Instance.LocalSkipList.Add(plugin);
        }

        var settings = YamlSettingsService.Instance;
        if (!settings.GetSetting<bool>("PACT Settings.yaml", "PACT_Settings.Debug Mode"))
        {
            await ClearXEditLogsAsync();
        }
    }

    private async Task<bool> ProcessLogLineAsync(string line, string plugin)
    {
        if (line.Contains("Undeleting:"))
        {
            await logService.LogToJournalAsync($"\n{plugin} -> Cleaned UDRs");
            Info.Instance.CleanResultsUdr.Add(plugin);
            return true;
        }
        if (line.Contains("Removing:"))
        {
            await logService.LogToJournalAsync($"\n{plugin} -> Cleaned ITMs");
            Info.Instance.CleanResultsItm.Add(plugin);
            return true;
        }
        if (line.Contains("Skipping:"))
        {
            await logService.LogToJournalAsync($"\n{plugin} -> Found Deleted Navmeshes");
            Info.Instance.CleanResultsNvm.Add(plugin);
            return true;
        }
        if (line.Contains("Making Partial Form:"))
        {
            await logService.LogToJournalAsync($"\n{plugin} -> Created Partial Forms");
            Info.Instance.CleanResultsPartialForms.Add(plugin);
            return true;
        }

        return false;
    }

    private async Task ReportCompletionAsync(DateTime startTime, int cleanedCount, int totalCount)
    {
        var elapsed = DateTime.Now - startTime;
        var message = $"Cleaning complete! Processed {cleanedCount}/{totalCount} plugins in {elapsed.TotalSeconds:F2} seconds.";
        await logService.LogToJournalAsync($"\n{message}");
        await logService.LogMessageAsync(message);
    }

    private async Task LogFailedPluginsAsync()
    {
        var categories = new[]
        {
            (Info.Instance.CleanFailedList, "Plugins that failed cleaning:"),
            (Info.Instance.CleanResultsUdr, "Plugins with Undisabled Records cleaned:"),
            (Info.Instance.CleanResultsItm, "Plugins with Identical To Master Records cleaned:"),
            (Info.Instance.CleanResultsNvm, "Caution: Plugins with Deleted Navmeshes."),
            (Info.Instance.CleanResultsPartialForms, "Plugins with ITMs converted to Partial Forms:")
        };

        foreach (var (plugins, message) in categories)
        {
            if (plugins.Any())
            {
                await logService.LogMessageAsync($"\n{message}");
                foreach (var plugin in plugins)
                {
                    await logService.LogMessageAsync(plugin);
                }
            }
        }
    }
}