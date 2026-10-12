using System.Windows;

namespace CalendarWork.App.Views;

/// <summary>잠금 모드에서 달력 오른쪽 위에 뜨는 잠금 해제 버튼. 위치·표시는 DesktopHost가 정한다.</summary>
public partial class LockBadgeWindow : Window
{
    public LockBadgeWindow()
    {
        InitializeComponent();
    }
}
