using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AutoQAC.ViewModels;
using AutoQAC.Services;
using AutoQAC.Models;
using MainWindow = AutoQAC.Views.MainWindow;

namespace AutoQAC;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Initialize configuration
            var configService = new ConfigurationService();
            var config = configService.LoadConfiguration();
            
            // Initialize core services
            var pluginInfo = new PluginInfo();
            var loggingService = new LoggingService(config, pluginInfo);
            var gameService = new GameService();
            var ignoreService = new IgnoreService();
            
            // Initialize cleaning service
            var cleaningService = new CleaningService(config, pluginInfo, loggingService);

            // Create and set up main window
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    loggingService,
                    config,
                    pluginInfo,
                    cleaningService,
                    gameService,
                    ignoreService)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}