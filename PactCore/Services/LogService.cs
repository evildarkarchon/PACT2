using System.Collections.ObjectModel;
using Avalonia.Threading;
using PACT2.PactCore.Models;

namespace PACT2.PactCore.Services;

public interface ILogService
{
    Task LogMessageAsync(string message);
    Task LogToJournalAsync(string message);
    Task ClearLogsAsync();
    ObservableCollection<string> LogMessages { get; }
}

public class LogService : ILogService
{
    private readonly string _journalPath = "PACT Journal.log";
    private readonly SemaphoreSlim _journalLock = new(1, 1);
    
    public ObservableCollection<string> LogMessages { get; } = new();

    public async Task LogMessageAsync(string message)
    {
        // Ensure UI updates happen on the UI thread
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            LogMessages.Add(message);
        });
    }

    public async Task LogToJournalAsync(string message)
    {
        await _journalLock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_journalPath, message);
        }
        finally
        {
            _journalLock.Release();
        }
    }

    public async Task ClearLogsAsync()
    {
        await _journalLock.WaitAsync();
        try
        {
            // Clear the journal file if it exists and is older than the expiration period
            if (File.Exists(_journalPath))
            {
                var fileInfo = new FileInfo(_journalPath);
                var age = DateTime.Now - fileInfo.LastWriteTime;
                if (age.Days > Info.Instance.JournalExpiration)
                {
                    File.Delete(_journalPath);
                }
            }

            // Clear the in-memory messages
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LogMessages.Clear();
            });
        }
        finally
        {
            _journalLock.Release();
        }
    }
}