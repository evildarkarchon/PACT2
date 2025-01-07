using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using PACT2.PactGui.Views;
using PACT2.PactCore.Services.Interfaces;

namespace PACT2.PactCore.Services;

public class DialogService : IDialogService
{
    private readonly Window _mainWindow;

    public DialogService()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainWindow = desktop.MainWindow ?? throw new InvalidOperationException("Main window not found");
        }
        else
        {
            throw new InvalidOperationException("Unsupported application lifetime");
        }
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new MessageBoxWindow
        {
            Title = title,
            DataContext = new MessageBoxViewModel
            {
                Message = message,
                ShowOkButton = true,
                ShowYesNoButtons = false
            }
        };
        await dialog.ShowDialog(_mainWindow);
    }

    public async Task ShowWarningAsync(string title, string message)
    {
        var dialog = new MessageBoxWindow
        {
            Title = title,
            DataContext = new MessageBoxViewModel
            {
                Message = message,
                ShowOkButton = true,
                ShowYesNoButtons = false
            }
        };
        await dialog.ShowDialog(_mainWindow);
    }

    public async Task ShowInfoAsync(string title, string message)
    {
        var dialog = new MessageBoxWindow
        {
            Title = title,
            DataContext = new MessageBoxViewModel
            {
                Message = message,
                ShowOkButton = true,
                ShowYesNoButtons = false
            }
        };
        await dialog.ShowDialog(_mainWindow);
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var dialog = new MessageBoxWindow
        {
            Title = title,
            DataContext = new MessageBoxViewModel
            {
                Message = message,
                ShowOkButton = false,
                ShowYesNoButtons = true
            }
        };
        
        var result = await dialog.ShowDialog<bool?>(_mainWindow);
        return result ?? false;
    }

    public async Task<string?> ShowOpenFileDialogAsync(string title, string? initialDirectory = null, params string[] filters)
    {
        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        if (filters.Length > 0)
        {
            var fileTypes = new List<FilePickerFileType>();
            foreach (var filter in filters)
            {
                var parts = filter.Split('|');
                if (parts.Length == 2)
                {
                    fileTypes.Add(new FilePickerFileType(parts[0])
                    {
                        Patterns = parts[1].Split(',').Select(x => $"*.{x.Trim('.')}").ToList()
                    });
                }
            }
            options.FileTypeFilter = fileTypes;
        }

        var files = await _mainWindow.StorageProvider.OpenFilePickerAsync(options);
        return files.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task<string?> ShowOpenFolderDialogAsync(string title, string? initialDirectory = null)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        var folders = await _mainWindow.StorageProvider.OpenFolderPickerAsync(options);
        return folders.FirstOrDefault()?.Path.LocalPath;
    }
}

public class MessageBoxViewModel
{
    public string Message { get; set; } = string.Empty;
    public bool ShowOkButton { get; set; }
    public bool ShowYesNoButtons { get; set; }
}