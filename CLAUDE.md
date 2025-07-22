# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AutoQAC (previously PACT2) is an Avalonia-based desktop application for automatically cleaning Bethesda game plugins using xEdit. It's a .NET 8 application using ReactiveUI for MVVM pattern implementation.

## Build and Run Commands

```bash
# Build the project
dotnet build

# Run the application
dotnet run

# Build for release
dotnet build -c Release

# Run tests (if available)
dotnet test
```

## Architecture

### Application Initialization
- Manual dependency injection in `App.axaml.cs`
- Service instantiation order: ConfigurationService → PluginInfo → LoggingService → GameService → IgnoreService → XEditProcessService → CleaningService
- MainWindowViewModel receives all services as dependencies

### MVVM with ReactiveUI
- **ViewModelBase**: Base class with reactive property patterns
- **MainWindowViewModel**: Primary ViewModel with reactive commands (`BrowseLoadOrderCommand`, `StartCleaningCommand`)
- **Reactive Properties**: Uses `this.RaiseAndSetIfChanged()` and `WhenAnyValue()` for change tracking
- **Observable Operations**: Progress reporting via `IObservable<CleaningProgress>`
- **ViewLocator**: Convention-based View-ViewModel mapping

### Service Layer
- **CleaningService**: Orchestrates xEdit cleaning with cancellation token support
- **XEditProcessService**: Process lifecycle management with proper termination
- **LoggingService**: Journal system with configurable expiration
- **ConfigurationService**: YAML serialization with YamlDotNet and underscore naming convention
- **GameService**: Auto-detects games from xEdit executable names using Mutagen
- **IgnoreService**: Game-specific plugin exclusion lists

### Configuration System
- **Primary**: `AutoQAC Settings.yaml` (created from `Data/Default Settings.yaml`)
- **Validation**: `ConfigurationValidator` for paths and settings
- **Key Settings**: CleaningTimeout (300s), JournalExpiration (7 days), PartialForms (experimental)

### Key Technical Details

- **xEdit Integration**: Uses command-line arguments `-QAC -autoexit -autoload` for automated cleaning
- **Timeout Handling**: Default 300-second timeout per plugin with cancellation support
- **Process Management**: Ensures xEdit processes are properly terminated
- **Logging**: Journal system with configurable expiration (default 7 days)
- **Game Support**: Multi-game support through xEdit variants (FO3Edit, FNVEdit, FO4Edit, SSEEdit)

### Key Dependencies

- **Avalonia 11.2.3**: Cross-platform UI framework with ReactiveUI integration
- **Mutagen.Bethesda 0.48.1**: Bethesda game file handling and plugin analysis
- **YamlDotNet 16.3.0**: Configuration serialization with underscore naming convention
- **.NET 8.0**: Target framework with nullable reference types enabled

## Important Implementation Details

### Service Dependencies
- Services must be instantiated in specific order due to dependencies
- All core services are injected into MainWindowViewModel constructor
- ConfigurationService loads settings from YAML on startup

### Reactive Programming Patterns
- Use `this.RaiseAndSetIfChanged()` for property setters in ViewModels
- Subscribe to property changes with `WhenAnyValue()`  
- Async operations return `IObservable<T>` for progress tracking

### xEdit Process Management
- xEdit processes must be properly terminated to prevent orphaned processes
- Use cancellation tokens for long-running operations
- Default timeout is 300 seconds per plugin

### Configuration Management
- Settings are auto-saved on application shutdown
- YAML files use underscore naming convention
- Validation occurs before saving configuration changes

## Development Notes

- Requires xEdit variants installed separately (FO3Edit, FNVEdit, FO4Edit, SSEEdit, etc.)
- Load order files (loadorder.txt/plugins.txt) must be accessible for game detection
- Experimental PartialForms feature requires xEdit >= 4.1.5b
- Primary platform support is Windows (`WinExe` with COM interop)