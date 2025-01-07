namespace PACT2.PactCore.Services.Interfaces;

public interface IDialogService
{
    Task ShowErrorAsync(string title, string message);
    Task ShowWarningAsync(string title, string message);
    Task ShowInfoAsync(string title, string message);
    Task<bool> ShowConfirmationAsync(string title, string message);
    Task<string?> ShowOpenFileDialogAsync(string title, string? initialDirectory = null, params string[] filters);
    Task<string?> ShowOpenFolderDialogAsync(string title, string? initialDirectory = null);
}