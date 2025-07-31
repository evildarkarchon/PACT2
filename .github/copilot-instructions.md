# AutoQAC AI Coding Instructions

AutoQAC is an Avalonia-based desktop application for automatically cleaning Bethesda game plugins using xEdit. It's a .NET 8 Windows application using ReactiveUI for MVVM patterns.

## Architecture Overview

### Manual Dependency Injection Pattern
- All services are instantiated manually in `App.axaml.cs` in a **specific order** due to dependencies
- Service instantiation sequence: `ConfigurationService` → `PluginInfo` → `LoggingService` → `GameService` → `IgnoreService` → `XEditProcessService` → `CleaningService`
- `MainWindowViewModel` receives all services as constructor dependencies
- No IoC container - services passed explicitly through constructors

### ReactiveUI MVVM Implementation
- **ViewModelBase**: All ViewModels inherit from this base class with reactive property patterns
- **Property Changes**: Always use `this.RaiseAndSetIfChanged(ref _field, value)` for property setters
- **Observable Subscriptions**: Use `WhenAnyValue()` for property change tracking and reactive bindings
- **Commands**: Use `ReactiveCommand.CreateFromTask()` for async operations, `ReactiveCommand.Create()` for sync
- **Progress Reporting**: Long-running operations return `IObservable<T>` (see `CleaningService.Progress`)

### xEdit Process Management (Critical Pattern)
- **Process Lifecycle**: xEdit processes are managed via `XEditProcessService` with proper termination
- **Timeout Handling**: Default 300-second timeout per plugin with `CancellationTokenSource.CreateLinkedTokenSource()`
- **Process Detection**: Game mode detection through xEdit executable names (FO3Edit, FNVEdit, FO4Edit, SSEEdit)
- **Orphan Prevention**: Always ensure processes are killed if not exited naturally
- **Command Line Args**: xEdit launched with `-QAC -autoexit -autoload` arguments

### Configuration System
- **YAML Serialization**: Uses YamlDotNet with `UnderscoredNamingConvention.Instance`
- **Default Config Pattern**: Copy from `Data/Default Settings.yaml` if `AutoQAC Settings.yaml` doesn't exist  
- **Validation**: `ConfigurationValidator.Validate()` before saving changes
- **Auto-save**: Configuration saved on application shutdown
- **Two-Stage Deserialization**: YAML → `ConfigurationData` → `AutoQacConfiguration` for proper mapping

## Key Technical Patterns

### xEdit Integration Specifics
- **Command Line Arguments**: Always use `-QAC -autoexit -autoload` when launching xEdit processes
- **Process Detection**: Game auto-detection via xEdit executable names (FO3Edit, FNVEdit, FO4Edit, SSEEdit)
- **Log File Monitoring**: Monitor both `xEdit_log.txt` and `xEditException.log` in xEdit directory
- **Orphan Process Prevention**: Use `XEditProcessService.EnsureXEditClosedAsync()` before starting new processes

### Game Support via Mutagen
- **Multi-game Support**: Through Mutagen.Bethesda for plugin analysis and environment detection
- **Game Release Detection**: `GameService.GetGameRelease()` tests compatibility for game variants (SSE/VR, FO4/VR)
- **Master Validation**: `GameService.HasMissingMasters()` for plugin dependency checking
- **Load Order Parsing**: Automatic detection from loadorder.txt/plugins.txt files

### Progress Reporting Pattern
- **Observable Streams**: Use `IObservable<CleaningProgress>` for real-time progress updates
- **Subject Pattern**: Services expose progress via `Subject<T>` and return `IObservable<T>`
- **UI Binding**: ViewModels subscribe to progress observables using `WhenAnyValue()` and `Subscribe()`
- **Example**: `CleaningService.Progress` reports current plugin being cleaned and overall status

### Logging & Journal System
- **Journal Expiration**: Configurable cleanup (default 7 days) via `LoggingService.ClearExpiredJournalAsync()`
- **xEdit Log Management**: Separate handling for xEdit_log.txt and xEditException.log files
- **Debug Mode**: Preserves logs when `config.DebugMode = true`

### UI Binding Conventions
- **ViewLocator**: Convention-based View-ViewModel mapping (replace "ViewModel" with "View")
- **Compiled Bindings**: Enabled by default with `AvaloniaUseCompiledBindingsByDefault`
- **DataType**: Always specify `x:DataType` in AXAML for strongly-typed bindings

## Development Workflow

### Build & Run Commands
```bash
dotnet build                # Build project
dotnet run                  # Run application  
dotnet build -c Release     # Release build
```

### Key Dependencies
- **Avalonia 11.2.3**: Cross-platform UI with ReactiveUI integration
- **Mutagen.Bethesda 0.48.1**: Bethesda plugin handling and game detection
- **YamlDotNet 16.3.0**: Configuration serialization with underscore naming

### Critical Implementation Rules
1. **Service Order**: Maintain dependency injection order in `App.axaml.cs`
2. **Process Safety**: Always use `XEditProcessService` for xEdit process management
3. **Reactive Properties**: Use `RaiseAndSetIfChanged` for all ViewModel properties
4. **Timeout Handling**: Implement cancellation tokens for long-running operations
5. **Error Messages**: Use constants from `Constants.cs` for standardized error/warning messages
6. **Fire-and-Forget**: Use `TaskExtensions.FireAndForget()` for background tasks that don't need awaiting

### Current Development Priorities (from TODO.txt)
- Improve xEdit progress tracking/reporting to UI
- Enhanced error message display
- Cleanup of orphaned xEdit processes

## File Structure Patterns
- **Services/**: Business logic and external integrations
- **Models/**: Data models and configuration classes  
- **ViewModels/**: ReactiveUI ViewModels with commands and observable properties
- **Views/**: Avalonia AXAML UI files
- **Data/**: Default configuration and ignore list templates
- **Extensions/**: Utility extension methods (like `TaskExtensions.FireAndForget()`)

When working on this codebase, always consider the xEdit process lifecycle, reactive property patterns, and the manual dependency injection order.
