using System;
using System.Runtime.InteropServices;
namespace EdgeTune;
internal static class Native
{
    public const int GWL_EXSTYLE = -20, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x80;
    public const int WM_HOTKEY = 0x312, WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] public static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] public static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("user32.dll")] public static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref CompositionData data);
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct AccentPolicy { public int State, Flags, Color, Animation; }
    [StructLayout(LayoutKind.Sequential)] public struct CompositionData { public int Attribute; public IntPtr Data; public int Size; }
    public static bool Glass(IntPtr hwnd, bool enabled)
    {
        try { var accent = new AccentPolicy { State = enabled ? 3 : 0 };
            var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<AccentPolicy>());
            try { Marshal.StructureToPtr(accent, pointer, false); var data = new CompositionData { Attribute = 19, Data = pointer, Size = Marshal.SizeOf<AccentPolicy>() }; return SetWindowCompositionAttribute(hwnd, ref data); }
            finally { Marshal.FreeHGlobal(pointer); }
        } catch { return false; }
    }
}
