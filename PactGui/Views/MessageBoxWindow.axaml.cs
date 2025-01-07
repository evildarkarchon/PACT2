using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PACT2.PactGui.Views;

public partial class MessageBoxWindow : Window
{
    public MessageBoxWindow()
    {
        InitializeComponent();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        Close(false);
    }
}