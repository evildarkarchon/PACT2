namespace AutoQAC.Models;

public class ConfigurationValidationResult
{
    public bool IsValid { get; }
    public string? Error { get; }

    private ConfigurationValidationResult(bool isValid, string? error = null)
    {
        IsValid = isValid;
        Error = error;
    }

    public static ConfigurationValidationResult Success() => new(true);
    public static ConfigurationValidationResult Failure(string error) => new(false, error);
}