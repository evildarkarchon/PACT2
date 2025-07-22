using Avalonia.Controls;
using Avalonia.Interactivity;
using Mutagen.Bethesda;

namespace AutoQAC.Views;

public partial class GameSelectionDialog : Window
{
    public GameRelease? SelectedGameRelease { get; private set; }

    public GameSelectionDialog()
    {
        InitializeComponent();
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        SelectedGameRelease = SteamRadioButton.IsChecked == true 
            ? GameRelease.SkyrimSE 
            : GameRelease.SkyrimSEGog;
        Close(SelectedGameRelease);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        SelectedGameRelease = null;
        Close(null);
    }
}