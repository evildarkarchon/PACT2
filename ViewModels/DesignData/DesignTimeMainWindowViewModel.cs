using System;
using AutoQAC.Models;
using AutoQAC.Services;

namespace AutoQAC.ViewModels.DesignData;

public class DesignTimeMainWindowViewModel : MainWindowViewModel
{
    public DesignTimeMainWindowViewModel() : base(
        new LoggingService(new AutoQacConfiguration(), new PluginInfo()),
        new AutoQacConfiguration(),
        CreateCleaningService(),
        new IgnoreService())
    {
        // Add some sample data for design-time preview
        var samplePlugin = new Plugin
        {
            Name = "SampleMod.esp",
            Path = @"C:\Games\Mods\SampleMod.esp",
            Description = "A sample mod",
            LastCleaned = DateTime.Now.AddDays(-1),
            LastCleaningResults = new CleaningResults
            {
                HasItm = true,
                HasUdr = false,
                HasNvm = false,
                HasPartialForms = false
            }
        };

        var anotherPlugin = new Plugin
        {
            Name = "AnotherMod.esp",
            Path = @"C:\Games\Mods\AnotherMod.esp",
            Description = "Another sample mod"
        };

        AvailablePlugins.Add(samplePlugin);
        AvailablePlugins.Add(anotherPlugin);

        LoadOrderPath = @"C:\Games\loadorder.txt";
        XEditPath = @"C:\Games\SSEEdit.exe";
        StatusMessage = "Ready to clean";
    }

    private static CleaningService CreateCleaningService()
    {
        var config = new AutoQacConfiguration();
        var pluginInfo = new PluginInfo();
        var loggingService = new LoggingService(config, pluginInfo);
        var xEditProcessService = new XEditProcessService(loggingService, config);

        return new CleaningService(config, pluginInfo, loggingService, xEditProcessService);
    }
}