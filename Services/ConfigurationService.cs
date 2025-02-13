// Services/ConfigurationService.cs

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using AutoQAC.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AutoQAC.Services;

public class ConfigurationService(
    string configPath = "AutoQAC Settings.yaml",
    string defaultConfigPath = "Data/Default Settings.yaml")
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
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
        private class AutoQacSection
        {
            public bool UpdateCheck { get; set; } = true;
            public bool StatLogging { get; set; } = true;
            public int CleaningTimeout { get; set; } = 300;
            public int JournalExpiration { get; set; } = 7;
            public string LoadOrderPath { get; set; } = string.Empty;
            public string XEditPath { get; set; } = string.Empty;
            public bool PartialForms { get; set; }
            public bool DebugMode { get; set; }
        }

        // ReSharper disable once PropertyCanBeMadeInitOnly.Local
        private AutoQacSection AutoQac { get; set; } = new();

        public AutoQacConfiguration ToConfiguration()
        {
            return new AutoQacConfiguration
            {
                UpdateCheck = AutoQac.UpdateCheck,
                StatLogging = AutoQac.StatLogging,
                CleaningTimeout = AutoQac.CleaningTimeout,
                JournalExpiration = AutoQac.JournalExpiration,
                LoadOrderPath = AutoQac.LoadOrderPath,
                XEditPath = AutoQac.XEditPath,
                PartialForms = AutoQac.PartialForms,
                DebugMode = AutoQac.DebugMode
            };
        }

        public static ConfigurationData FromConfiguration(AutoQacConfiguration config)
        {
            return new ConfigurationData
            {
                AutoQac = new AutoQacSection
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