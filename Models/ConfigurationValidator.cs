using System;
using System.IO;

namespace AutoQAC.Models;

public static class ConfigurationValidator
{
    public static ConfigurationValidationResult Validate(AutoQacConfiguration config)
    {
        // Validate timeout
        if (config.CleaningTimeout < 30)
        {
            return ConfigurationValidationResult.Failure(
                "Cleaning timeout must be at least 30 seconds.");
        }

        // Validate journal expiration
        if (config.JournalExpiration < 1)
        {
            return ConfigurationValidationResult.Failure(
                "Journal expiration must be at least 1 day.");
        }

        // Only validate paths if they're set (they might be empty on first run)
        if (!string.IsNullOrEmpty(config.XEditPath))
        {
            if (!File.Exists(config.XEditPath))
            {
                return ConfigurationValidationResult.Failure(
                    $"xEdit executable not found at path: {config.XEditPath}");
            }

            if (!config.XEditPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return ConfigurationValidationResult.Failure(
                    "xEdit path must point to an executable (.exe) file.");
            }
        }

        if (!string.IsNullOrEmpty(config.LoadOrderPath))
        {
            if (!File.Exists(config.LoadOrderPath))
            {
                return ConfigurationValidationResult.Failure(
                    $"Load order file not found at path: {config.LoadOrderPath}");
            }

            // Check if it's a text file
            var extension = Path.GetExtension(config.LoadOrderPath);
            if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return ConfigurationValidationResult.Failure(
                    "Load order path must point to a text (.txt) file.");
            }
        }

        return ConfigurationValidationResult.Success();
    }
}