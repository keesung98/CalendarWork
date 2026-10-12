using System.Runtime.InteropServices;

namespace CalendarWork.Interop;

/// <summary>
/// 물리 키 상태. WPF의 Keyboard.Modifiers는 창이 활성일 때 받은 키만 반영하므로,
/// 비활성 창을 Ctrl+클릭하는 경우를 위해 실제 키 상태를 본다.
/// </summary>
public static partial class KeyboardState
{
    private const int VK_CONTROL = 0x11;

    public static bool IsControlDown => (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vKey);
}
