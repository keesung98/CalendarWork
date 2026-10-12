using System.Windows;

namespace CalendarWork.App.Views;

/// <summary>설정 창. DataContext는 XAML에서 ViewModelLocator.Settings로 연결한다.</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }
}
