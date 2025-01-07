using YamlDotNet.Serialization;
using PACT2.PactCore.Services.Interfaces;

namespace PACT2.PactCore.Services;

public class YamlSettingsService
{
    private static readonly Dictionary<string, object?> YamlCache = new();
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().Build();
    private static readonly ISerializer YamlSerializer = new SerializerBuilder().Build();

    // ReSharper disable once InconsistentNaming
    private static readonly Lazy<YamlSettingsService> _instance = 
        new(() => new YamlSettingsService());

    private readonly IDialogService _dialogService;

    // Private constructor to prevent direct instantiation
    private YamlSettingsService()
    {
        _dialogService = new DialogService(); // Instantiating the dialog service here
    }

    // Singleton instance property
    public static YamlSettingsService Instance => _instance.Value;

    public T? GetSetting<T>(string yamlPath, string keyPath, T? defaultValue = default)
    {
        try
        {
            if (!YamlCache.ContainsKey(yamlPath))
            {
                using var reader = new StreamReader(yamlPath);
                YamlCache[yamlPath] = YamlDeserializer.Deserialize(reader);
            }

            var data = YamlCache[yamlPath];
            var keys = keyPath.Split('.');
            var value = data;

            foreach (var key in keys)
            {
                if (value is IDictionary<object, object> dict && dict.TryGetValue(key, out var value1))
                {
                    value = value1;
                }
                else
                {
                    return defaultValue;
                }
            }

            if (value is T typedValue)
            {
                return typedValue;
            }

            return defaultValue;
        }
        catch (Exception ex)
        {
            _dialogService.ShowErrorAsync("YAML Settings Error", ex.Message);
            return defaultValue;
        }
    }

    public void SetSetting<T>(string yamlPath, string keyPath, T? value)
    {
        try
        {
            if (!YamlCache.ContainsKey(yamlPath))
            {
                if (!File.Exists(yamlPath))
                {
                    File.WriteAllText(yamlPath, "");
                }
                using var reader = new StreamReader(yamlPath);
                YamlCache[yamlPath] = YamlDeserializer.Deserialize(reader) ?? new Dictionary<object, object>();
            }

            var data = YamlCache[yamlPath];
            var keys = keyPath.Split('.');
            var current = data;

            // Navigate to the correct location in the YAML structure
            for (int i = 0; i < keys.Length - 1; i++)
            {
                var key = keys[i];
                if (current is IDictionary<object, object> dict)
                {
                    if (!dict.ContainsKey(key))
                    {
                        dict[key] = new Dictionary<object, object>();
                    }
                    current = dict[key];
                }
            }

            // Set the value
            if (current is IDictionary<object, object> finalDict)
            {
                finalDict[keys[^1]] = value!;
            }

            // Write back to file
            using var writer = new StreamWriter(yamlPath);
            YamlSerializer.Serialize(writer, data);

            // Update cache
            YamlCache[yamlPath] = data;
        }
        catch (Exception ex)
        {
            _dialogService.ShowErrorAsync("YAML Settings Error", ex.Message);
        }
    }
}