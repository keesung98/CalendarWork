using System.Windows;
using Microsoft.Win32;

namespace CalendarWork.App.Services;

public sealed class DialogService : IDialogService
{
    private const string Title = "CalendarWork";

    // 현재 활성 창(설정 창 등)을 소유자로 삼아 그 위에 띄운다
    private static Window? Owner => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

    public string? ShowSaveFile(string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = defaultFileName };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? ShowOpenFile(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public bool Confirm(string message) =>
        Show(message, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;

    public void ShowInfo(string message) => Show(message, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string message) => Show(message, MessageBoxButton.OK, MessageBoxImage.Error);

    private static MessageBoxResult Show(string message, MessageBoxButton buttons, MessageBoxImage image) =>
        Owner is { } owner
            ? MessageBox.Show(owner, message, Title, buttons, image)
            : MessageBox.Show(message, Title, buttons, image);
}
