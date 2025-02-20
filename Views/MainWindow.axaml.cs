using Avalonia.Controls;
using AutoQAC.ViewModels;

namespace AutoQAC.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Initialize(StorageProvider);
        }
    }
}