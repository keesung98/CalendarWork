using System.Windows.Interop;
using static CalendarWork.Interop.NativeMethods;

namespace CalendarWork.Interop;

/// <summary>잠금 모드에서 창을 둘 바탕화면 레이어.</summary>
public enum DesktopLayer
{
    /// <summary>
    /// 기본: Progman 소유(owned) 최하단 창 + 클릭 투과.
    /// 소유 창은 항상 소유자 바로 위에 있으므로 Win+D(바탕화면 보기)에도 사라지지 않는다.
    /// 아이콘보다는 위에 그려지지만 클릭은 아이콘으로 통과한다.
    /// </summary>
    DesktopOwned,

    /// <summary>
    /// WorkerW 자식: 아이콘 뒤·배경화면 위. 구버전 Windows용.
    /// Windows 11 24H2+ (Progman이 DirectComposition 사용)에서는 부착은 성공해도 WPF 창이 그려지지 않는다
    /// (25H2 실기 확인).
    /// </summary>
    WorkerW,

    /// <summary>폴백: 소유자 없는 최하단 창 + 클릭 투과. Win+D에서는 숨겨진다.</summary>
    BottomMost,
}

public enum DesktopAttachState
{
    /// <summary>일반 최상위 창 (편집 모드).</summary>
    Detached,
    DesktopOwned,
    WorkerW,
    BottomMost,
}

/// <summary>
/// 창을 바탕화면 레이어에 붙이거나 떼어 낸다. 실패하면 <see cref="DesktopLayer.BottomMost"/>로 폴백한다.
/// </summary>
public sealed class DesktopAttacher
{
    private const uint SpawnWorkerWMessage = 0x052C;
    private const int GWLP_HWNDPARENT = -8;

    private readonly nint _hwnd;
    private nint _originalOwner;
    private bool _hookInstalled;
    private bool _keepBottom;

    public DesktopAttacher(nint hwnd)
    {
        _hwnd = hwnd;
    }

    public DesktopAttachState State { get; private set; } = DesktopAttachState.Detached;

    /// <summary>잠금 모드로 전환. 실제로 적용된 상태를 반환한다.</summary>
    public DesktopAttachState Attach(DesktopLayer layer)
    {
        if (State != DesktopAttachState.Detached)
            Detach();

        State = layer switch
        {
            DesktopLayer.DesktopOwned when TryAttachOwned() => DesktopAttachState.DesktopOwned,
            DesktopLayer.WorkerW when TryAttachToWorkerW() => DesktopAttachState.WorkerW,
            _ => AttachBottomMost(),
        };
        return State;
    }

    /// <summary>편집 모드로 전환. 화면상 위치를 유지한 채 일반 최상위 창으로 되돌린다.</summary>
    public void Detach()
    {
        switch (State)
        {
            case DesktopAttachState.WorkerW:
                GetWindowRect(_hwnd, out var rect);
                uint style = GetStyle(_hwnd, GWL_STYLE);
                SetStyle(_hwnd, GWL_STYLE, (style & ~WS_CHILD) | WS_POPUP);
                SetParent(_hwnd, 0);
                SetWindowPos(_hwnd, 0, rect.Left, rect.Top, rect.Width, rect.Height,
                    SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                break;

            case DesktopAttachState.DesktopOwned:
                SetWindowLongPtr(_hwnd, GWLP_HWNDPARENT, _originalOwner);
                _keepBottom = false;
                SetClickThrough(false);
                break;

            case DesktopAttachState.BottomMost:
                _keepBottom = false;
                SetClickThrough(false);
                break;
        }
        State = DesktopAttachState.Detached;
    }

    public static nint FindProgman() => FindWindow("Progman", null);

    public static nint FindWorkerW()
    {
        nint progman = FindProgman();
        if (progman == 0)
            return 0;

        // WorkerW 생성 유도 (설계값 0xD/0x1, 실패 시 0/0으로 재시도)
        SendMessageTimeout(progman, SpawnWorkerWMessage, 0xD, 0x1, SMTO_NORMAL, 1000, out _);
        nint found = FindWorkerWCore(progman);
        if (found != 0)
            return found;

        SendMessageTimeout(progman, SpawnWorkerWMessage, 0, 0, SMTO_NORMAL, 1000, out _);
        return FindWorkerWCore(progman);
    }

    private static nint FindWorkerWCore(nint progman)
    {
        // 경로 1: Windows 11 24H2+ — Progman의 자식 WorkerW (SHELLDLL_DefView보다 뒤)
        nint child = FindWindowEx(progman, 0, "WorkerW", null);
        if (child != 0)
            return child;

        // 경로 2: 이전 구조 — SHELLDLL_DefView를 자식으로 가진 최상위 창의 "다음" WorkerW
        nint top = 0;
        while ((top = FindWindowEx(0, top, null, null)) != 0)
        {
            if (FindWindowEx(top, 0, "SHELLDLL_DefView", null) != 0)
                return FindWindowEx(0, top, "WorkerW", null);
        }
        return 0;
    }

    private bool TryAttachOwned()
    {
        nint progman = FindProgman();
        if (progman == 0)
            return false;

        _originalOwner = GetWindowLongPtr(_hwnd, GWLP_HWNDPARENT);
        SetWindowLongPtr(_hwnd, GWLP_HWNDPARENT, progman);
        KeepAtBottom();
        return true;
    }

    private bool TryAttachToWorkerW()
    {
        nint workerW = FindWorkerW();
        if (workerW == 0)
            return false;

        GetWindowRect(_hwnd, out var rect);

        // SetParent 전에 WS_POPUP → WS_CHILD
        uint style = GetStyle(_hwnd, GWL_STYLE);
        SetStyle(_hwnd, GWL_STYLE, (style & ~WS_POPUP) | WS_CHILD);
        if (SetParent(_hwnd, workerW) == 0 && GetParent(_hwnd) != workerW)
        {
            SetStyle(_hwnd, GWL_STYLE, style);
            return false;
        }

        var pt = new POINT { X = rect.Left, Y = rect.Top };
        ScreenToClient(workerW, ref pt);
        SetWindowPos(_hwnd, 0, pt.X, pt.Y, rect.Width, rect.Height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        return true;
    }

    private DesktopAttachState AttachBottomMost()
    {
        KeepAtBottom();
        return DesktopAttachState.BottomMost;
    }

    private void KeepAtBottom()
    {
        if (!_hookInstalled)
        {
            HwndSource.FromHwnd(_hwnd)?.AddHook(BottomMostHook);
            _hookInstalled = true;
        }
        _keepBottom = true;
        SetClickThrough(true);
        SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>WM_WINDOWPOSCHANGING을 가로채 Z-order를 항상 최하단으로 유지.</summary>
    private unsafe nint BottomMostHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (_keepBottom && msg == WM_WINDOWPOSCHANGING)
        {
            var pos = (WINDOWPOS*)lParam;
            pos->hwndInsertAfter = HWND_BOTTOM;
            pos->flags &= ~SWP_NOZORDER;
        }
        return 0;
    }

    /// <summary>WS_EX_TRANSPARENT로 마우스를 아래(바탕화면 아이콘)로 통과시킨다.</summary>
    public void SetClickThrough(bool enabled)
    {
        uint ex = GetStyle(_hwnd, GWL_EXSTYLE);
        ex = enabled ? ex | WS_EX_TRANSPARENT | WS_EX_LAYERED : ex & ~WS_EX_TRANSPARENT;
        SetStyle(_hwnd, GWL_EXSTYLE, ex);
    }

    /// <summary>Alt+Tab 목록에서 숨긴다.</summary>
    public static void HideFromAltTab(nint hwnd)
    {
        SetStyle(hwnd, GWL_EXSTYLE, GetStyle(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
    }
}
