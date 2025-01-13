// ViewModels/MainWindowViewModel.cs

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

public class MainWindowViewModel : ReactiveObject, IActivatableViewModel
{
    private readonly AutoQacConfiguration _config;
    private readonly CleaningService _cleaningService;
    private CancellationTokenSource? _cleaningCts;

    private int _progress;
    private int _maxProgress;
    private string _statusMessage = string.Empty;
    private bool _isCleaning;

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

    public bool IsCleaning
    {
        get => _isCleaning;
        private set => this.RaiseAndSetIfChanged(ref _isCleaning, value);
    }

    public ReactiveCommand<Unit, Unit> StartCleaningCommand { get; }

    public MainWindowViewModel()
    {
        Activator = new ViewModelActivator();

        _config = LoadConfiguration();
        var pluginInfo = new PluginInfo();
        _cleaningService = new CleaningService(_config, pluginInfo);

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
        return !string.IsNullOrEmpty(_config.XEditPath) && 
               !string.IsNullOrEmpty(_config.LoadOrderPath) &&
               File.Exists(_config.XEditPath) &&
               File.Exists(_config.LoadOrderPath);
    }

    private void UpdateProgress(CleaningProgress progress)
    {
        Progress = progress.Current;
        MaxProgress = progress.Total;
        StatusMessage = progress.Message;
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
        }
        finally
        {
            IsCleaning = false;
            _cleaningCts?.Dispose();
            _cleaningCts = null;
        }
    }

    private static AutoQacConfiguration LoadConfiguration()
    {
        // TODO: Load from settings file
        return new AutoQacConfiguration
        {
            XEditPath = "",
            LoadOrderPath = "",
            CleaningTimeout = 300,
            JournalExpiration = 7,
            UpdateCheck = true
        };
    }
}