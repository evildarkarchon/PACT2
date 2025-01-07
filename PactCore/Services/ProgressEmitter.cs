using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PACT2.PactCore.Services;

public class ProgressEmitter : INotifyPropertyChanged
{
    private const string ProgressMessageTemplate = "Cleaning {0} {1}/{2} - {3}%";

    private int _progress;
    private int _maxValue;
    private string _pluginValue = string.Empty;
    private bool _isVisible;
    private bool _taskCompleted;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Progress
    {
        get => _progress;
        set
        {
            if (_progress != value)
            {
                _progress = value;
                OnPropertyChanged();
            }
        }
    }

    public int MaxValue
    {
        get => _maxValue;
        set
        {
            if (_maxValue != value)
            {
                _maxValue = value;
                OnPropertyChanged();
            }
        }
    }

    public string PluginValue
    {
        get => _pluginValue;
        set
        {
            if (_pluginValue != value)
            {
                _pluginValue = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged();
            }
        }
    }

    public bool TaskCompleted
    {
        get => _taskCompleted;
        set
        {
            if (_taskCompleted != value)
            {
                _taskCompleted = value;
                OnPropertyChanged();
            }
        }
    }

    public void EmitMaxValue(int maxValue)
    {
        MaxValue = maxValue;
    }

    public void ReportProgress(int count)
    {
        Progress = count;
    }

    public void ReportPlugin(string plugin)
    {
        PluginValue = string.Format(ProgressMessageTemplate, plugin, Progress, MaxValue, 
            MaxValue > 0 ? (Progress * 100 / MaxValue) : 0);
    }

    public void ReportDone()
    {
        TaskCompleted = true;
    }

    public void SetVisibility(bool value = true)
    {
        IsVisible = value;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}