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
            // Initialize services
            var configService = new ConfigurationService();
            var config = configService.LoadConfiguration();
            var pluginInfo = new PluginInfo();
            var loggingService = new LoggingService(config, pluginInfo);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(loggingService, configService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}