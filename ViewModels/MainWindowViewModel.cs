using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Linq;
using Avalonia.Collections;
using Avalonia.Platform.Storage;
using ReactiveUI;
using AutoQAC.Models;
using AutoQAC.Services;
using AutoQAC.Extensions;
using AutoQAC.Views;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Avalonia.Controls;

namespace AutoQAC.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly AutoQacConfiguration _config;
    private readonly CleaningService _cleaningService;
    private readonly LoggingService _loggingService;
    private readonly IgnoreService _ignoreService;
    private IStorageProvider? _storageProvider;
    private Window? _parentWindow;
    private GameRelease? _detectedGameRelease;

    // Observable collections and state
    public ObservableCollection<Plugin> AvailablePlugins { get; } = [];
    public AvaloniaList<int> PluginSelection { get; } = [];

    private string _loadOrderPath = string.Empty;
    private string _xEditPath = string.Empty;
    private string _statusMessage = "Select load order location and xEdit executable to begin...";
    private string _emptyMessage = "No plugins available";
    private string _actionButtonText = "Start Cleaning";
    private bool _isCleaning;
    private bool _canStartCleaning;
    private CancellationTokenSource? _cleaningCancellation;
    private int _currentProgress;
    private int _totalProgress;
    private double _progressPercentage;

    public string LoadOrderPath
    {
        get => _loadOrderPath;
        set => this.RaiseAndSetIfChanged(ref _loadOrderPath, value);
    }

    public string XEditPath
    {
        get => _xEditPath;
        set => this.RaiseAndSetIfChanged(ref _xEditPath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public string EmptyMessage
    {
        get => _emptyMessage;
        set => this.RaiseAndSetIfChanged(ref _emptyMessage, value);
    }

    public string ActionButtonText
    {
        get => _actionButtonText;
        set => this.RaiseAndSetIfChanged(ref _actionButtonText, value);
    }

    public bool HasPlugins => AvailablePlugins.Any();

    public bool CanStartCleaning
    {
        get => _canStartCleaning;
        set => this.RaiseAndSetIfChanged(ref _canStartCleaning, value);
    }

    public int CurrentProgress
    {
        get => _currentProgress;
        set => this.RaiseAndSetIfChanged(ref _currentProgress, value);
    }

    public int TotalProgress
    {
        get => _totalProgress;
        set => this.RaiseAndSetIfChanged(ref _totalProgress, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => this.RaiseAndSetIfChanged(ref _progressPercentage, value);
    }

    public bool IsProgressVisible => _isCleaning && TotalProgress > 0;

    // Commands
    public ReactiveCommand<Unit, Unit> BrowseLoadOrderCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseXEditCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectNoneCommand { get; }
    public ReactiveCommand<Unit, Unit> StartCleaningCommand { get; }

    public MainWindowViewModel(
        LoggingService loggingService,
        AutoQacConfiguration config,
        CleaningService cleaningService,
        IgnoreService ignoreService)
    {
        _loggingService = loggingService;
        _config = config;
        _cleaningService = cleaningService;
        _ignoreService = ignoreService;

        // Initialize commands
        BrowseLoadOrderCommand = ReactiveCommand.CreateFromTask(BrowseLoadOrderAsync);
        BrowseXEditCommand = ReactiveCommand.CreateFromTask(BrowseXEditAsync);
        SelectAllCommand = ReactiveCommand.Create(SelectAll);
        SelectNoneCommand = ReactiveCommand.Create(SelectNone);
        StartCleaningCommand = ReactiveCommand.CreateFromTask(StartCleaningAsync);

        // Load saved paths
        LoadOrderPath = _config.LoadOrderPath;
        XEditPath = _config.XEditPath;

        // Subscribe to path changes
        this.WhenAnyValue(x => x.LoadOrderPath)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(_ => LoadPluginsAsync().FireAndForget());

        this.WhenAnyValue(x => x.XEditPath)
            .Subscribe(_ => UpdateCanStartCleaning());

        // Subscribe to selection changes
        PluginSelection.CollectionChanged += (_, _) => UpdateCanStartCleaning();

        // Subscribe to cleaning progress
        _cleaningService.Progress
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(OnProgressUpdated);
    }

    public void Initialize(IStorageProvider storageProvider, Window? parentWindow = null)
    {
        _storageProvider = storageProvider;
        _parentWindow = parentWindow;
    }

    private void OnProgressUpdated(CleaningProgress progress)
    {
        CurrentProgress = progress.Current;
        TotalProgress = progress.Total;
        StatusMessage = progress.Message;
        
        if (TotalProgress > 0)
        {
            ProgressPercentage = (double)CurrentProgress / TotalProgress * 100;
        }
        else
        {
            ProgressPercentage = 0;
        }

        this.RaisePropertyChanged(nameof(IsProgressVisible));
    }

    private async Task LoadPluginsAsync()
    {
        if (string.IsNullOrEmpty(LoadOrderPath) || !File.Exists(LoadOrderPath)) return;

        try
        {
            AvailablePlugins.Clear();

            // If we have a detected GameRelease from executable selection, use Mutagen
            if (_detectedGameRelease.HasValue)
            {
                await LoadPluginsWithMutagenAsync(_detectedGameRelease.Value);
            }
            else
            {
                // Fall back to old method for compatibility
                await LoadPluginsLegacyAsync();
            }

            if (!AvailablePlugins.Any())
            {
                EmptyMessage = "No plugins available for cleaning";
            }

            UpdateCanStartCleaning();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading plugins: {ex.Message}";
            await _loggingService.LogToJournalAsync($"Error loading plugins: {ex.Message}");
        }
    }

    private async Task LoadPluginsWithMutagenAsync(GameRelease gameRelease)
    {
        try
        {
            var env = GameEnvironment.Typical.Construct(gameRelease);
            
            // Try to get ignore list, but don't let it block the plugin loading
            List<string> ignoreList = [];
            try
            {
                ignoreList = _ignoreService.GetIgnoreListForGameRelease(gameRelease);
                await _loggingService.LogToJournalAsync($"Successfully loaded ignore list for {gameRelease}");
            }
            catch (Exception ex)
            {
                await _loggingService.LogToJournalAsync($"Warning: Could not load ignore list for {gameRelease}: {ex.Message}. Continuing without ignore list.");
                // Continue with empty ignore list - this is non-fatal
            }

            foreach (var modListing in env.LoadOrder.ListedOrder)
            {
                if (modListing.Ghosted || modListing.Mod == null) continue;

                var pluginName = modListing.ModKey.FileName;
                
                // Skip ignored plugins using case-insensitive and partial matching
                if (_ignoreService.ShouldIgnorePlugin(pluginName, gameRelease)) continue;

                // Check if plugin is empty (has no records to clean)
                var hasRecords = modListing.Mod.EnumerateMajorRecords().Any();
                if (!hasRecords) continue;

                var plugin = new Plugin
                {
                    Name = pluginName,
                    Path = modListing.Mod.ModKey.FileName // Mutagen handles the full path
                };

                AvailablePlugins.Add(plugin);
            }

            await _loggingService.LogToJournalAsync($"Loaded {AvailablePlugins.Count} plugins using Mutagen for {gameRelease}");
        }
        catch (Exception ex)
        {
            await _loggingService.LogToJournalAsync($"Error loading plugins with Mutagen: {ex.Message}");
            throw;
        }
    }

    private async Task LoadPluginsLegacyAsync()
    {
        var gameMode = GameService.DetectGameMode(LoadOrderPath);

        if (gameMode == null)
        {
            EmptyMessage = "Unable to detect game mode from load order";
            return;
        }

        // Try to get ignore list, but don't let it block the plugin loading
        List<string> ignoreList = [];
        try
        {
            ignoreList = _ignoreService.GetIgnoreList(gameMode);
            await _loggingService.LogToJournalAsync($"Successfully loaded ignore list for {gameMode}");
        }
        catch (Exception ex)
        {
            await _loggingService.LogToJournalAsync($"Warning: Could not load ignore list for {gameMode}: {ex.Message}. Continuing without ignore list.");
            // Continue with empty ignore list - this is non-fatal
        }

        var loadOrderContent = await File.ReadAllLinesAsync(LoadOrderPath);
        var plugins = loadOrderContent
            .Skip(1) // Skip first line
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => new Plugin
            {
                Name = Path.GetFileName(line.Replace("*", "").Trim()),
                Path = Path.Combine(Path.GetDirectoryName(LoadOrderPath) ?? "", line.Replace("*", "").Trim())
            })
            .Where(p => !_ignoreService.ShouldIgnorePlugin(p.Name, gameMode)) // Case-insensitive and partial matching
            .ToList();

        foreach (var plugin in plugins.Where(plugin => !GameService.IsMutagenSupported(gameMode) ||
                                                       !GameService.IsEmptyPlugin(plugin.Name)))
        {
            AvailablePlugins.Add(plugin);
        }

        await _loggingService.LogToJournalAsync($"Loaded {AvailablePlugins.Count} plugins using legacy method for {gameMode}");
    }

    private async Task BrowseLoadOrderAsync()
    {
        try
        {
            if (_storageProvider == null) return;

            var filePickerOptions = new FilePickerOpenOptions
            {
                Title = "Select Load Order File or Game Directory",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("All Supported Files")
                    {
                        Patterns = ["*.txt", "*.exe"]
                    },
                    new FilePickerFileType("Text Files")
                    {
                        Patterns = ["*.txt"]
                    },
                    new FilePickerFileType("Executables")
                    {
                        Patterns = ["*.exe"]
                    }
                ]
            };

            var result = await _storageProvider.OpenFilePickerAsync(filePickerOptions);
            if (result.Count > 0)
            {
                var selectedPath = result[0].Path.LocalPath;
                var fileExtension = Path.GetExtension(selectedPath).ToLowerInvariant();
                
                if (fileExtension == ".exe")
                {
                    await HandleExecutableSelectionAsync(selectedPath);
                }
                else
                {
                    // Clear any previously detected GameRelease when manually selecting a text file
                    _detectedGameRelease = null;
                    LoadOrderPath = selectedPath;
                    _config.LoadOrderPath = LoadOrderPath;
                }
            }
        }
        catch (Exception ex)
        {
            await _loggingService.LogToJournalAsync($"Error in file selection: {ex.Message}");
        }
    }

    private async Task HandleExecutableSelectionAsync(string executablePath)
    {
        var gameRelease = GameService.DetectGameReleaseFromExecutable(executablePath);
        
        if (gameRelease == null)
        {
            // Special case for Skyrim Special Edition - requires user selection
            var fileName = Path.GetFileName(executablePath).ToLowerInvariant();
            
            if (fileName == "skyrim.exe" || fileName == "skyrimse.exe")
            {
                gameRelease = await ShowSkyrimEditionSelectionAsync();
                if (gameRelease == null) return; // User cancelled
            }
            else
            {
                await _loggingService.LogToJournalAsync($"Could not determine game from executable: {executablePath}");
                return;
            }
        }

        // Use Mutagen to detect the load order file for this game
        try
        {
            var env = GameEnvironment.Typical.Construct(gameRelease.Value);
            
            // Try to find the loadorder.txt or plugins.txt file based on the game directory
            var gameDir = Path.GetDirectoryName(executablePath);
            if (gameDir == null)
            {
                await _loggingService.LogToJournalAsync($"Could not determine game directory from: {executablePath}");
                return;
            }

            // Look for common load order files in various locations
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var potentialPaths = new[]
            {
                // Game directory and parent directory
                Path.Combine(gameDir, "loadorder.txt"),
                Path.Combine(gameDir, "plugins.txt"),
                Path.Combine(gameDir, "..", "loadorder.txt"),
                Path.Combine(gameDir, "..", "plugins.txt"),
                
                // AppData locations for different games
                Path.Combine(appData, GetGameAppDataFolder(gameRelease.Value), "loadorder.txt"),
                Path.Combine(appData, GetGameAppDataFolder(gameRelease.Value), "plugins.txt"),
                
                // Bethesda Game Studios folder (some games)
                Path.Combine(appData, "Bethesda Game Studios", GetGameDataFolderName(gameRelease.Value), "loadorder.txt"),
                Path.Combine(appData, "Bethesda Game Studios", GetGameDataFolderName(gameRelease.Value), "plugins.txt")
            };

            string? foundLoadOrderPath = null;
            foreach (var potentialPath in potentialPaths)
            {
                var normalizedPath = Path.GetFullPath(potentialPath);
                if (File.Exists(normalizedPath))
                {
                    foundLoadOrderPath = normalizedPath;
                    break;
                }
            }

            if (foundLoadOrderPath != null)
            {
                _detectedGameRelease = gameRelease.Value;
                LoadOrderPath = foundLoadOrderPath;
                _config.LoadOrderPath = LoadOrderPath;
                await _loggingService.LogToJournalAsync($"Detected {gameRelease} and loaded order from: {foundLoadOrderPath}");
            }
            else
            {
                await _loggingService.LogToJournalAsync($"Load order file not found for {gameRelease}.");
            }
        }
        catch (Exception ex)
        {
            await _loggingService.LogToJournalAsync($"Error detecting load order for {gameRelease}: {ex.Message}");
        }
    }

    private static string GetGameAppDataFolder(GameRelease gameRelease)
    {
        return gameRelease switch
        {
            GameRelease.SkyrimSE => "Skyrim Special Edition",
            GameRelease.SkyrimSEGog => "Skyrim Special Edition", 
            GameRelease.SkyrimVR => "Skyrim VR",
            GameRelease.Fallout4 => "Fallout4",
            GameRelease.Fallout4VR => "Fallout4VR", 
            GameRelease.Oblivion => "Oblivion",
            _ => gameRelease.ToString()
        };
    }

    private static string GetGameDataFolderName(GameRelease gameRelease)
    {
        return gameRelease switch
        {
            GameRelease.SkyrimSE => "Skyrim Special Edition",
            GameRelease.SkyrimSEGog => "Skyrim Special Edition GOG", 
            GameRelease.SkyrimVR => "Skyrim VR",
            GameRelease.Fallout4 => "Fallout4",
            GameRelease.Fallout4VR => "Fallout4VR",
            GameRelease.Oblivion => "Oblivion",
            _ => gameRelease.ToString()
        };
    }

    /// <summary>
    /// Converts a GameRelease to the corresponding game mode string for backward compatibility.
    /// </summary>
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

    private async Task<GameRelease?> ShowSkyrimEditionSelectionAsync()
    {
        if (_parentWindow == null)
        {
            // Fallback if no parent window available
            await _loggingService.LogToJournalAsync("Skyrim Special Edition detected - defaulting to Steam version (no dialog available).");
            return GameRelease.SkyrimSE;
        }

        try
        {
            var dialog = new GameSelectionDialog();
            var result = await dialog.ShowDialog<GameRelease?>(_parentWindow);
            
            if (result != null)
            {
                var editionName = result == GameRelease.SkyrimSE ? "Steam" : "GOG";
                await _loggingService.LogToJournalAsync($"User selected Skyrim Special Edition ({editionName}).");
            }
            else
            {
                await _loggingService.LogToJournalAsync("User cancelled game edition selection.");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            await _loggingService.LogToJournalAsync($"Error showing game selection dialog: {ex.Message}");
            return GameRelease.SkyrimSE; // Fallback to Steam version
        }
    }

    private async Task BrowseXEditAsync()
    {
        if (_storageProvider == null) return;

        var filePickerOptions = new FilePickerOpenOptions
        {
            Title = "Select xEdit Executable",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Executables")
                {
                    Patterns = ["*.exe"]
                }
            ]
        };

        var result = await _storageProvider.OpenFilePickerAsync(filePickerOptions);
        if (result.Count > 0)
        {
            XEditPath = result[0].Path.LocalPath;
            _config.XEditPath = XEditPath;
        }
    }

    private void SelectAll()
    {
        PluginSelection.Clear();
        for (var i = 0; i < AvailablePlugins.Count; i++)
        {
            PluginSelection.Add(i);
        }
    }

    private void SelectNone()
    {
        PluginSelection.Clear();
    }

    private void UpdateCanStartCleaning()
    {
        CanStartCleaning = !string.IsNullOrEmpty(XEditPath) &&
                           !string.IsNullOrEmpty(LoadOrderPath) &&
                           PluginSelection.Count > 0 &&
                           !_isCleaning;
    }

    private async Task StartCleaningAsync()
    {
        if (_isCleaning)
        {
            await _cleaningCancellation?.CancelAsync()!;
            return;
        }

        try
        {
            _isCleaning = true;
            ActionButtonText = "Cancel";
            CurrentProgress = 0;
            TotalProgress = 0;
            ProgressPercentage = 0;
            this.RaisePropertyChanged(nameof(IsProgressVisible));
            UpdateCanStartCleaning();

            _cleaningCancellation = new CancellationTokenSource();

            var selectedPlugins = PluginSelection
                .Select(index => AvailablePlugins[index])
                .ToList();

            // Get the game mode - use detected GameRelease if available
            string? gameMode = null;
            if (_detectedGameRelease.HasValue)
            {
                gameMode = ConvertGameReleaseToGameMode(_detectedGameRelease.Value);
            }
            else
            {
                gameMode = GameService.DetectGameMode(_config.LoadOrderPath);
                if (gameMode == null)
                {
                    StatusMessage = "Error: Could not detect game mode from load order";
                    return;
                }
            }

            // Use the new batch cleaning method with progress tracking
            await _cleaningService.CleanPluginsAsync(
                selectedPlugins,
                gameMode,
                _cleaningCancellation.Token);

            if (!_cleaningCancellation.Token.IsCancellationRequested)
            {
                StatusMessage = "Cleaning complete";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error during cleaning: {ex.Message}";
            await _loggingService.LogToJournalAsync($"Error during cleaning: {ex.Message}");
        }
        finally
        {
            _isCleaning = false;
            ActionButtonText = "Start Cleaning";
            CurrentProgress = 0;
            TotalProgress = 0;
            ProgressPercentage = 0;
            this.RaisePropertyChanged(nameof(IsProgressVisible));
            _cleaningCancellation?.Dispose();
            _cleaningCancellation = null;
            UpdateCanStartCleaning();
        }
    }
}