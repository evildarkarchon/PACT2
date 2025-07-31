using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoQAC.Models;

namespace AutoQAC.Services;

public class XEditProcessService(
    LoggingService loggingService,
    AutoQacConfiguration config)
{
    public async Task<bool> IsXEditRunningAsync(string gameMode)
    {
        try
        {
            var xEditProcesses = Process.GetProcesses()
                .Where(p => IsXEditProcess(p, gameMode))
                .ToList();

            if (!xEditProcesses.Any()) return false;

            await loggingService.LogToJournalAsync(
                $"Found existing xEdit process for {gameMode}. Cannot start cleaning until it is closed.");

            return true;
        }
        catch (Exception ex)
        {
            await loggingService.LogToJournalAsync($"Error checking xEdit processes: {ex.Message}");
            throw;
        }
    }

    public async Task EnsureXEditClosedAsync(string gameMode)
    {
        try
        {
            var xEditProcesses = Process.GetProcesses()
                .Where(p => IsXEditProcess(p, gameMode));

            foreach (var process in xEditProcesses)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();
                        await Task.Delay(1000); // Give it a chance to close gracefully
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            await loggingService.LogToJournalAsync($"Error closing xEdit processes: {ex.Message}");
            throw;
        }
    }

    private bool IsXEditProcess(Process process, string gameMode)
    {
        try
        {
            if (process.HasExited) return false;

            var processName = Path.GetFileNameWithoutExtension(process.ProcessName.ToLower());
            var xEditName = Path.GetFileNameWithoutExtension(config.XEditPath).ToLower();

            if (processName != xEditName) return false;

            // For universal xEdit
            if (processName == "xedit") return true;

            // For game-specific xEdit executables
            return gameMode.ToLower() switch
            {
                "sse" => processName.Contains("sseedit") || processName.Contains("tes5edit") ||
                         processName.Contains("tes5edit"),
                "fo4" => processName.Contains("fo4edit"),
                "fo3" => processName.Contains("fo3edit"),
                "fnv" => processName.Contains("fnvedit"),
                "tes4" => processName.Contains("tes4edit"),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
}