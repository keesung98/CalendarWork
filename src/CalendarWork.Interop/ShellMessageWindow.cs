using System.Windows.Interop;
using static CalendarWork.Interop.NativeMethods;

namespace CalendarWork.Interop;

[Flags]
public enum HotKeyModifiers : uint
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
    NoRepeat = 0x4000,
}

public enum TrayMouseAction
{
    LeftClick,
    RightClick,
}

/// <summary>
/// 보이지 않는 최상위 창 하나로 트레이 아이콘·전역 단축키·셸 브로드캐스트를 처리한다.
/// 메인 창은 WorkerW 자식이 될 수 있어 브로드캐스트(TaskbarCreated)를 받지 못하므로 분리했다.
/// 트레이는 WinForms NotifyIcon 대신 Shell_NotifyIcon을 직접 호출한다 (WinForms 로드 방지).
/// </summary>
public sealed class ShellMessageWindow : IDisposable
{
    private const uint TrayCallbackMessage = WM_APP + 1;
    private const uint TrayIconId = 1;

    private readonly HwndSource _source;
    private readonly uint _taskbarCreatedMessage;
    private readonly HashSet<int> _hotKeyIds = [];
    private nint _icon;
    private string _tooltip = "";
    private bool _trayAdded;

    public ShellMessageWindow()
    {
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        _source = new HwndSource(new HwndSourceParameters("CalendarWork.ShellMessageWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = (int)WS_EX_TOOLWINDOW,
        });
        _source.AddHook(WndProc);
    }

    public nint Handle => _source.Handle;

    public event Action<int>? HotKeyPressed;
    public event Action<TrayMouseAction>? TrayMouse;

    /// <summary>탐색기가 재시작되어 작업 표시줄이 다시 만들어졌을 때.</summary>
    public event Action? ExplorerRestarted;

    public event Action? DisplayChanged;

    public bool RegisterHotKey(int id, HotKeyModifiers modifiers, uint virtualKey)
    {
        if (!NativeMethods.RegisterHotKey(Handle, id, (uint)(modifiers | HotKeyModifiers.NoRepeat), virtualKey))
            return false;
        _hotKeyIds.Add(id);
        return true;
    }

    public void UnregisterHotKey(int id)
    {
        if (_hotKeyIds.Remove(id))
            NativeMethods.UnregisterHotKey(Handle, id);
    }

    /// <summary>.ico 파일로 트레이 아이콘을 추가한다.</summary>
    public void ShowTrayIcon(string iconPath, string tooltip)
    {
        if (_icon == 0)
        {
            _icon = LoadImage(0, iconPath, IMAGE_ICON,
                GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), LR_LOADFROMFILE);
        }
        _tooltip = tooltip;
        AddOrModifyTray(NIM_ADD);
    }

    public void SetTrayTooltip(string tooltip)
    {
        _tooltip = tooltip;
        if (_trayAdded)
            AddOrModifyTray(NIM_MODIFY);
    }

    /// <summary>트레이 메뉴를 띄우기 전에 호출해야 바깥 클릭 시 메뉴가 닫힌다.</summary>
    public static void BringToForeground(nint hwnd) => SetForegroundWindow(hwnd);

    private unsafe void AddOrModifyTray(uint message)
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = Handle,
            uID = TrayIconId,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = _icon,
        };
        var tip = _tooltip.AsSpan(0, Math.Min(_tooltip.Length, 127));
        tip.CopyTo(new Span<char>(data.szTip, 128));

        bool ok = Shell_NotifyIcon(message, ref data);
        if (message == NIM_ADD)
            _trayAdded = ok;
    }

    private unsafe void RemoveTray()
    {
        if (!_trayAdded)
            return;
        var data = new NOTIFYICONDATAW { cbSize = (uint)sizeof(NOTIFYICONDATAW), hWnd = Handle, uID = TrayIconId };
        Shell_NotifyIcon(NIM_DELETE, ref data);
        _trayAdded = false;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            HotKeyPressed?.Invoke((int)wParam);
            handled = true;
        }
        else if (msg == (int)TrayCallbackMessage)
        {
            switch ((int)(lParam & 0xFFFF))
            {
                case WM_LBUTTONUP: TrayMouse?.Invoke(TrayMouseAction.LeftClick); break;
                case WM_RBUTTONUP: TrayMouse?.Invoke(TrayMouseAction.RightClick); break;
            }
            handled = true;
        }
        else if (msg == (int)_taskbarCreatedMessage && _taskbarCreatedMessage != 0)
        {
            if (_icon != 0)
                AddOrModifyTray(NIM_ADD);
            ExplorerRestarted?.Invoke();
        }
        else if (msg == WM_DISPLAYCHANGE)
        {
            DisplayChanged?.Invoke();
        }
        return 0;
    }

    public void Dispose()
    {
        RemoveTray();
        foreach (int id in _hotKeyIds)
            NativeMethods.UnregisterHotKey(Handle, id);
        _hotKeyIds.Clear();
        if (_icon != 0)
        {
            DestroyIcon(_icon);
            _icon = 0;
        }
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
