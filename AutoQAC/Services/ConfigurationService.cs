// Services/ConfigurationService.cs

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using AutoQAC.Models;
using YamlDotNet.Serialization;

namespace AutoQAC.Services;

public class ConfigurationService(
    string configPath = "AutoQAC Settings.yaml",
    string defaultConfigPath = "Data/Default Settings.yaml")
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .Build();

    private readonly ISerializer _serializer = new SerializerBuilder()
        .Build();

    public AutoQacConfiguration LoadConfiguration()
    {
        try
        {
            if (!File.Exists(configPath))
            {
                if (!File.Exists(defaultConfigPath))
                {
                    throw new FileNotFoundException("Default configuration file not found", defaultConfigPath);
                }

                File.Copy(defaultConfigPath, configPath);
            }

            var yaml = File.ReadAllText(configPath);
            var configData = _deserializer.Deserialize<ConfigurationData>(yaml);
            var config = configData.ToConfiguration();

            var validationResult = ConfigurationValidator.Validate(config);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Invalid configuration: {validationResult.Error}");
            }

            return config;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to load configuration", ex);
        }
    }

    private void SaveConfiguration(AutoQacConfiguration config)
    {
        try
        {
            var validationResult = ConfigurationValidator.Validate(config);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Invalid configuration: {validationResult.Error}");
            }

            var configData = ConfigurationData.FromConfiguration(config);
            var yaml = _serializer.Serialize(configData);
            File.WriteAllText(configPath, yaml);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to save configuration", ex);
        }
    }

    public void UpdateConfiguration(Action<AutoQacConfiguration> updateAction)
    {
        var config = LoadConfiguration();
        updateAction(config);

        var validationResult = ConfigurationValidator.Validate(config);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Invalid configuration: {validationResult.Error}");
        }

        SaveConfiguration(config);
    }

    private class ConfigurationData
    {
        [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Local")]
        public class AutoQacSection
        {
            [YamlMember(Alias = "Update Check")]
            public bool UpdateCheck { get; set; } = true;
            
            [YamlMember(Alias = "Stat Logging")]
            public bool StatLogging { get; set; } = true;
            
            [YamlMember(Alias = "Cleaning Timeout")]
            public int CleaningTimeout { get; set; } = 300;
            
            [YamlMember(Alias = "Journal Expiration")]
            public int JournalExpiration { get; set; } = 7;
            
            [YamlMember(Alias = "LoadOrder TXT")]
            public string LoadOrderPath { get; set; } = string.Empty;
            
            [YamlMember(Alias = "XEDIT EXE")]
            public string XEditPath { get; set; } = string.Empty;
            
            [YamlMember(Alias = "Partial Forms")]
            public bool PartialForms { get; set; }
            
            [YamlMember(Alias = "Debug Mode")]
            public bool DebugMode { get; set; }
        }

        // ReSharper disable once PropertyCanBeMadeInitOnly.Local
        public AutoQacSection Settings { get; set; } = new();

        public AutoQacConfiguration ToConfiguration()
        {
            return new AutoQacConfiguration
            {
                UpdateCheck = Settings.UpdateCheck,
                StatLogging = Settings.StatLogging,
                CleaningTimeout = Settings.CleaningTimeout,
                JournalExpiration = Settings.JournalExpiration,
                LoadOrderPath = Settings.LoadOrderPath,
                XEditPath = Settings.XEditPath,
                PartialForms = Settings.PartialForms,
                DebugMode = Settings.DebugMode
            };
        }

        public static ConfigurationData FromConfiguration(AutoQacConfiguration config)
        {
            return new ConfigurationData
            {
                Settings = new AutoQacSection
                {
                    UpdateCheck = config.UpdateCheck,
                    StatLogging = config.StatLogging,
                    CleaningTimeout = config.CleaningTimeout,
                    JournalExpiration = config.JournalExpiration,
                    LoadOrderPath = config.LoadOrderPath,
                    XEditPath = config.XEditPath,
                    PartialForms = config.PartialForms,
                    DebugMode = config.DebugMode
                }
            };
        }
    }
}