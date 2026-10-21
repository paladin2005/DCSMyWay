using System.Runtime.InteropServices;

namespace DCSMyWay.Interop;

internal static class NativeMethods
{
  public const int GwlExStyle = -20;
  public const int GwlpHwndParent = -8;
  public const nint WsExNoActivate = 0x08000000;
  public const int WhKeyboardLl = 13;
  public const int WmKeyDown = 0x0100;
  public const int WmKeyUp = 0x0101;
  public const int WmSysKeyDown = 0x0104;
  public const int WmSysKeyUp = 0x0105;
  public const uint LlkhfInjected = 0x10;
  public static readonly Guid SavedGamesFolder = new("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4");

  public delegate nint LowLevelKeyboardProc(int code, nint message, nint data);

  [StructLayout(LayoutKind.Sequential)]
  public struct KbdLlHookStruct
  {
    public uint VkCode;
    public uint ScanCode;
    public uint Flags;
    public uint Time;
    public nuint ExtraInfo;
  }

  [DllImport("user32.dll")]
  public static extern uint GetDpiForWindow(nint hwnd);

  [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
  public static extern nint GetWindowLongPtr(nint hwnd, int index);

  [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
  public static extern nint SetWindowLongPtr(nint hwnd, int index, nint newLong);

  [DllImport("user32.dll")]
  public static extern nint GetForegroundWindow();

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static extern bool SetForegroundWindow(nint hwnd);

  [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
  public static extern int GetWindowText(nint hwnd, char[] text, int maxCount);

  [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
  public static extern nint SetWindowsHookEx(int hookId, LowLevelKeyboardProc callback, nint module, uint threadId);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static extern bool UnhookWindowsHookEx(nint hook);

  [DllImport("user32.dll")]
  public static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

  [DllImport("user32.dll")]
  public static extern short GetAsyncKeyState(int virtualKey);

  [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
  public static extern nint GetModuleHandle(string? moduleName);

  [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
  public static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid folderId, uint flags, nint token, out string path);
}
