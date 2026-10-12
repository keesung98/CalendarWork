using System.Windows;

namespace CalendarWork.App.Views;

/// <summary>바탕화면 달력 창. DataContext·동작은 모두 XAML(ViewModelLocator, Behaviors)에서 연결한다.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
