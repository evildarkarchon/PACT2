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

/// <summary>
/// Represents the ViewModel for the main application window, implementing
/// functionality and state management for the user interface.
/// </summary>
/// <remarks>
/// The MainWindowViewModel provides reactive binding properties and commands
/// to interact with the user interface layer. It manages the application's configuration,
/// cleaning workflow progress, and state transitions.
/// </remarks>
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

    /// <summary>
    /// Represents the ViewModel for the main application window, providing state management
    /// and reactive bindings for the user interface. It supports configuration validation,
    /// cleaning operations, and workflow progress updates.
    /// </summary>
    /// <remarks>
    /// This class is responsible for orchestrating the interaction flow between the UI
    /// and underlying services such as the cleaning process and configuration handling. It
    /// utilizes the ReactiveUI framework to support reactive property handling and commands.
    /// </remarks>
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

    /// <summary>
    /// Determines whether the current configuration is valid by verifying the presence
    /// and accessibility of required file paths in the configuration.
    /// </summary>
    /// <returns>
    /// A boolean value indicating whether the configuration is valid. Returns true
    /// if the required file paths are non-empty and point to existing files; otherwise, false.
    /// </returns>
    private bool IsConfigurationValid()
    {
        return !string.IsNullOrEmpty(_config.XEditPath) && 
               !string.IsNullOrEmpty(_config.LoadOrderPath) &&
               File.Exists(_config.XEditPath) &&
               File.Exists(_config.LoadOrderPath);
    }

    /// <summary>
    /// Updates the UI properties related to the cleaning operation's progress, including
    /// the current progress value, maximum progress, and a status message.
    /// </summary>
    /// <param name="progress">
    /// An instance of <see cref="CleaningProgress"/> containing the current progress value,
    /// the total progress, and a message associated with the progress state.
    /// </param>
    /// <remarks>
    /// This method is invoked whenever there's an update in the cleaning process progress,
    /// allowing the user interface to reflect real-time changes in the cleaning workflow.
    /// </remarks>
    private void UpdateProgress(CleaningProgress progress)
    {
        Progress = progress.Current;
        MaxProgress = progress.Total;
        StatusMessage = progress.Message;
    }

    /// <summary>
    /// Initiates the asynchronous cleaning process for plugins, utilizing the configured cleaning service.
    /// This method handles cancellations, updates cleaning state, and manages error reporting.
    /// </summary>
    /// <remarks>
    /// This method is invoked by the StartCleaningCommand and ensures thread safety while modifying
    /// the cleaning state. It also ensures proper resource cleanup and status updates upon completion
    /// or error occurrences.
    /// </remarks>
    /// <returns>
    /// A Task representing the ongoing cleaning operation, completing when the process finishes
    /// or an exception is thrown.
    /// </returns>
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