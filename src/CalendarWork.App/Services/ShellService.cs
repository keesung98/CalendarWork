using System.Windows;
using CalendarWork.App.Views;

namespace CalendarWork.App.Services;

/// <summary>설정 창은 하나만 띄운다. 창의 DataContext는 XAML(ViewModelLocator)에서 정해진다.</summary>
public sealed class ShellService : IShellService
{
    private SettingsWindow? _settingsWindow;

    public void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        _settingsWindow.Activate();
    }

    public void Shutdown() => Application.Current.Shutdown();
}
