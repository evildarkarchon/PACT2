using System;
using System.IO;
using ReactiveUI;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using AutoQAC.Services;
using AutoQAC.Models;

namespace AutoQAC.ViewModels;

public class MainWindowViewModel : ReactiveObject, IActivatableViewModel, IDisposable
{
    private readonly AutoQacConfiguration _config;
    private readonly PluginInfo _pluginInfo;
    private readonly CleaningService _cleaningService;
    private readonly LoggingService _loggingService;
    private readonly ConfigurationService _configService;
    private readonly UpdateService _updateService;
    private CancellationTokenSource? _cleaningCts;
    private bool _disposed;

    private int _progress;
    private int _maxProgress;
    private string _statusMessage = string.Empty;
    private string? _updateMessage;
    private bool _isCleaning;
    private string _xEditPath = string.Empty;
    private string _loadOrderPath = string.Empty;
    private bool _debugMode;
    private bool _partialForms;
    private bool _updateCheck = true;

    public ViewModelActivator Activator { get; }

    public int Progress
    {
        get => _progress;
        private set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    public int MaxProgress
    {
        get => _maxProgress;
        private set => this.RaiseAndSetIfChanged(ref _maxProgress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public string? UpdateMessage
    {
        get => _updateMessage;
        private set => this.RaiseAndSetIfChanged(ref _updateMessage, value);
    }

    public bool IsCleaning
    {
        get => _isCleaning;
        private set => this.RaiseAndSetIfChanged(ref _isCleaning, value);
    }

    public string XEditPath
    {
        get => _xEditPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _xEditPath, value);
            SaveConfiguration();
        }
    }

    public string LoadOrderPath
    {
        get => _loadOrderPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _loadOrderPath, value);
            SaveConfiguration();
        }
    }

    public bool DebugMode
    {
        get => _debugMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _debugMode, value);
            SaveConfiguration();
        }
    }

    public bool PartialForms
    {
        get => _partialForms;
        set
        {
            this.RaiseAndSetIfChanged(ref _partialForms, value);
            SaveConfiguration();
        }
    }

    public bool UpdateCheck
    {
        get => _updateCheck;
        set
        {
            this.RaiseAndSetIfChanged(ref _updateCheck, value);
            SaveConfiguration();
        }
    }

    public ReactiveCommand<Unit, Unit> StartCleaningCommand { get; }

    public MainWindowViewModel(LoggingService loggingService, ConfigurationService configService)
    {
        Activator = new ViewModelActivator();

        _configService = configService;
        _config = _configService.LoadConfiguration();
        _pluginInfo = new PluginInfo();
        _loggingService = loggingService;
        _cleaningService = new CleaningService(_config, _pluginInfo, _loggingService);
        _updateService = new UpdateService(_loggingService);

        // Initialize properties from configuration
        XEditPath = _config.XEditPath;
        LoadOrderPath = _config.LoadOrderPath;
        DebugMode = _config.DebugMode;
        PartialForms = _config.PartialForms;
        UpdateCheck = _config.UpdateCheck;

        if (_config.UpdateCheck)
        {
            // Fire and forget - we don't want to block startup
            _ = CheckForUpdatesAsync();
        }

        var canStartCleaning = this.WhenAnyValue(x => x.IsCleaning)
            .Select(cleaning => !cleaning && IsConfigurationValid());

        StartCleaningCommand = ReactiveCommand.CreateFromTask(
            StartCleaningAsync,
            canStartCleaning);

        this.WhenActivated(disposables =>
        {
            _cleaningService.Progress
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(UpdateProgress)
                .DisposeWith(disposables);
        });
    }

    private bool IsConfigurationValid()
    {
        return !string.IsNullOrEmpty(XEditPath) && 
               !string.IsNullOrEmpty(LoadOrderPath) &&
               File.Exists(XEditPath) &&
               File.Exists(LoadOrderPath);
    }

    private void UpdateProgress(CleaningProgress progress)
    {
        Progress = progress.Current;
        MaxProgress = progress.Total;
        StatusMessage = progress.Message;
    }

    private void SaveConfiguration()
    {
        _configService.UpdateConfiguration(config =>
        {
            config.XEditPath = XEditPath;
            config.LoadOrderPath = LoadOrderPath;
            config.DebugMode = DebugMode;
            config.PartialForms = PartialForms;
            config.UpdateCheck = UpdateCheck;
        });
    }

    private async Task StartCleaningAsync()
    {
        if (IsCleaning)
        {
            _cleaningCts?.Cancel();
            return;
        }

        try
        {
            IsCleaning = true;
            _cleaningCts = new CancellationTokenSource();
            
            await _cleaningService.CleanPluginsAsync(_cleaningCts.Token);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            await _loggingService.LogToJournalAsync($"Error during cleaning: {ex.Message}");
        }
        finally
        {
            IsCleaning = false;
            _cleaningCts?.Dispose();
            _cleaningCts = null;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            UpdateMessage = result.Message;
        }
        catch (Exception ex)
        {
            await _loggingService.LogToJournalAsync($"Error checking for updates: {ex.Message}");
            UpdateMessage = "Failed to check for updates";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _updateService.Dispose();
        _disposed = true;
    }

    // Finalizer as a backup
    ~MainWindowViewModel()
    {
        Dispose();
    }
}