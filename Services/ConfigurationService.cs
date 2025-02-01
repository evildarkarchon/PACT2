// Services/ConfigurationService.cs
using System;
using System.IO;
using AutoQAC.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AutoQAC.Services;

/// <summary>
/// Provides functionality to manage application configuration, including loading, saving,
/// and updating configuration settings stored in YAML files. Utilizes YAML serialization
/// and deserialization to handle configuration data efficiently.
/// </summary>
public class ConfigurationService
{
    private readonly string _configPath;
    private readonly string _defaultConfigPath;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;
    
    private static readonly string DefaultConfigContent = @"
auto_qac:
  update_check: true
  stat_logging: true
  cleaning_timeout: 300
  journal_expiration: 7
  load_order_path: ''
  xedit_path: ''
  partial_forms: false
  debug_mode: false
";

    /// <summary>
    /// Provides functionality to manage application configuration, including loading, saving,
    /// and updating configuration settings stored in YAML files. Utilizes YAML serialization
    /// and deserialization to handle configuration data efficiently.
    /// </summary>
    public ConfigurationService(
        string configPath = "AutoQAC Settings.yaml", 
        string defaultConfigPath = "Data/Default Settings.yaml")
    {
        _configPath = configPath;
        _defaultConfigPath = defaultConfigPath;
        
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
            
        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    /// Loads the application configuration from a YAML file. If the configuration file does not
    /// exist, it creates a new one using a default configuration template or predefined default settings.
    /// </summary>
    /// <returns>
    /// An instance of <c>AutoQacConfiguration</c> representing the loaded or generated configuration settings.
    /// </returns>
    public AutoQacConfiguration LoadConfiguration()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                if (File.Exists(_defaultConfigPath))
                {
                    File.Copy(_defaultConfigPath, _configPath);
                }
                else
                {
                    File.WriteAllText(_configPath, DefaultConfigContent);
                }
            }

            var yaml = File.ReadAllText(_configPath);
            var configData = _deserializer.Deserialize<ConfigurationData>(yaml);
            return configData.ToConfiguration();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to load configuration", ex);
        }
    }

    /// <summary>
    /// Saves the provided configuration object to a YAML file. This method serializes the
    /// configuration data and writes it to the configured file path, ensuring persistence
    /// of application settings.
    /// </summary>
    /// <param name="config">The configuration object containing application settings to save.</param>
    public void SaveConfiguration(AutoQacConfiguration config)
    {
        try
        {
            var configData = ConfigurationData.FromConfiguration(config);
            var yaml = _serializer.Serialize(configData);
            File.WriteAllText(_configPath, yaml);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to save configuration", ex);
        }
    }

    /// <summary>
    /// Updates specific values in the application configuration and persists the changes.
    /// </summary>
    /// <param name="updateAction">A delegate that modifies the configuration. The provided
    /// action takes an instance of <c>AutoQacConfiguration</c> as input and applies updates
    /// to its properties.</param>
    public void UpdateConfiguration(Action<AutoQacConfiguration> updateAction)
    {
        var config = LoadConfiguration();
        updateAction(config);
        SaveConfiguration(config);
    }

    // Internal class for YAML serialization that matches the expected YAML structure
    /// <summary>
    /// Represents the internal data structure used for YAML serialization and deserialization
    /// of configuration settings in the application. Acts as a mapping between the application
    /// configuration model and its YAML representation.
    /// </summary>
    private class ConfigurationData
    {
        /// <summary>
        /// Represents a configuration section pertaining to AutoQAC settings.
        /// This class is used for managing various user-configurable options, such as enabling update checks,
        /// statistical logging, cleaning timeout periods, journal expiration,
        /// and paths to necessary external resources or files.
        /// </summary>
        public class AutoQacSection
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

        public AutoQacSection AutoQac { get; set; } = new();

        /// <summary>
        /// Converts the internal ConfigurationData object into an AutoQacConfiguration object,
        /// mapping the deserialized YAML configuration values to the application's configuration model.
        /// </summary>
        /// <returns>An instance of AutoQacConfiguration containing the mapped configuration settings.</returns>
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

        /// <summary>
        /// Creates a new instance of <see cref="ConfigurationData"/> from the provided
        /// <see cref="AutoQacConfiguration"/> object. Facilitates transformation of the application's
        /// configuration model into an intermediary data structure for YAML serialization.
        /// </summary>
        /// <param name="config">The source configuration object containing application settings to be converted.</param>
        /// <returns>A populated <see cref="ConfigurationData"/> object representing the application's configuration settings.</returns>
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