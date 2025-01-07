using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PACT2.PactGui.ViewModels;

public class MessageBoxViewModel : INotifyPropertyChanged
{
    private string _message = string.Empty;
    private bool _showOkButton;
    private bool _showYesNoButtons;

    public string Message
    {
        get => _message;
        set
        {
            if (_message != value)
            {
                _message = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowOkButton
    {
        get => _showOkButton;
        set
        {
            if (_showOkButton != value)
            {
                _showOkButton = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowYesNoButtons
    {
        get => _showYesNoButtons;
        set
        {
            if (_showYesNoButtons != value)
            {
                _showYesNoButtons = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}