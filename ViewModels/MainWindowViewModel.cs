using System;
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

namespace AutoQAC.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly AutoQacConfiguration _config;
    private readonly CleaningService _cleaningService;
    private readonly LoggingService _loggingService;
    private readonly IgnoreService _ignoreService;
    private IStorageProvider? _storageProvider;

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
    }

    public void Initialize(IStorageProvider storageProvider)
    {
        _storageProvider = storageProvider;
    }

    private async Task LoadPluginsAsync()
    {
        if (string.IsNullOrEmpty(LoadOrderPath) || !File.Exists(LoadOrderPath)) return;

        try
        {
            AvailablePlugins.Clear();
            var gameMode = GameService.DetectGameMode(LoadOrderPath);

            if (gameMode == null)
            {
                EmptyMessage = "Unable to detect game mode from load order";
                return;
            }

            // Get ignore list for the game
            var ignoreList = _ignoreService.GetIgnoreList(gameMode);

            var loadOrderContent = await File.ReadAllLinesAsync(LoadOrderPath);
            var plugins = loadOrderContent
                .Skip(1) // Skip first line
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => new Plugin
                {
                    Name = Path.GetFileName(line.Replace("*", "").Trim()),
                    Path = Path.Combine(Path.GetDirectoryName(LoadOrderPath) ?? "", line.Replace("*", "").Trim())
                })
                .Where(p => !ignoreList.Contains(p.Name))
                .ToList();

            foreach (var plugin in plugins.Where(plugin => !GameService.IsMutagenSupported(gameMode) ||
                                                           !GameService.IsEmptyPlugin(plugin.Name)))
            {
                AvailablePlugins.Add(plugin);
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

    private async Task BrowseLoadOrderAsync()
    {
        if (_storageProvider == null) return;

        var filePickerOptions = new FilePickerOpenOptions
        {
            Title = "Select Load Order File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Text Files")
                {
                    Patterns = ["*.txt"]
                }
            ]
        };

        var result = await _storageProvider.OpenFilePickerAsync(filePickerOptions);
        if (result.Count > 0)
        {
            LoadOrderPath = result[0].Path.LocalPath;
            _config.LoadOrderPath = LoadOrderPath;
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
            UpdateCanStartCleaning();

            _cleaningCancellation = new CancellationTokenSource();

            var selectedPlugins = PluginSelection
                .Select(index => AvailablePlugins[index])
                .ToList();

            // Get the game mode from the load order
            var gameMode = GameService.DetectGameMode(_config.LoadOrderPath);
            if (gameMode == null)
            {
                StatusMessage = "Error: Could not detect game mode from load order";
                return;
            }

            foreach (var plugin in selectedPlugins)
            {
                if (_cleaningCancellation.Token.IsCancellationRequested)
                {
                    StatusMessage = "Cleaning cancelled";
                    break;
                }

                StatusMessage = $"Cleaning {plugin.Name}...";
                await _cleaningService.CleanPluginAsync(
                    plugin.Name,
                    gameMode,
                    _cleaningCancellation.Token);
            }

            StatusMessage = "Cleaning complete";
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
            _cleaningCancellation?.Dispose();
            _cleaningCancellation = null;
            UpdateCanStartCleaning();
        }
    }
}