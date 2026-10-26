using System.ComponentModel;
using System.Runtime.InteropServices;
using DCSMyWay.Interop;
using DCSMyWay.Models;
using Windows.System;

namespace DCSMyWay.Services;

public sealed class KeyboardHook : IDisposable
{
  private static readonly HashSet<VirtualKey> ModifierKeys =
  [
    VirtualKey.Control, VirtualKey.LeftControl, VirtualKey.RightControl,
    VirtualKey.Shift, VirtualKey.LeftShift, VirtualKey.RightShift,
    VirtualKey.Menu, VirtualKey.LeftMenu, VirtualKey.RightMenu,
    VirtualKey.LeftWindows, VirtualKey.RightWindows,
  ];

  private readonly NativeMethods.LowLevelKeyboardProc _callback;
  private readonly Func<KeyPress, bool> _onKeyPressed;
  private readonly HashSet<uint> _pressedKeys = [];
  private readonly HashSet<uint> _swallowedKeys = [];
  private nint _hook;

  public KeyboardHook(Func<KeyPress, bool> onKeyPressed)
  {
    _onKeyPressed = onKeyPressed;
    _callback = OnKeyboardEvent;
  }

  public void Start()
  {
    if (_hook != 0) return;
    _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _callback, NativeMethods.GetModuleHandle(null), 0);
    if (_hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
  }

  public void Dispose()
  {
    if (_hook == 0) return;
    NativeMethods.UnhookWindowsHookEx(_hook);
    _hook = 0;
  }

  private nint OnKeyboardEvent(int code, nint message, nint data)
  {
    if (code >= 0)
    {
      var keyEvent = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(data);
      switch ((int)message)
      {
        case NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown:
          if (OnKeyDown(keyEvent)) return 1;
          break;
        case NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp:
          _pressedKeys.Remove(keyEvent.VkCode);
          if (_swallowedKeys.Remove(keyEvent.VkCode)) return 1;
          break;
      }
    }
    return NativeMethods.CallNextHookEx(_hook, code, message, data);
  }

  private bool OnKeyDown(NativeMethods.KbdLlHookStruct keyEvent)
  {
    if (!_pressedKeys.Add(keyEvent.VkCode)) return _swallowedKeys.Contains(keyEvent.VkCode);
    var key = (VirtualKey)keyEvent.VkCode;
    if (ModifierKeys.Contains(key) || IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows)) return false;
    var injected = (keyEvent.Flags & NativeMethods.LlkhfInjected) != 0;
    if (!_onKeyPressed(new KeyPress(key, IsDown(VirtualKey.Control), IsDown(VirtualKey.Shift), IsDown(VirtualKey.Menu), injected))) return false;
    _swallowedKeys.Add(keyEvent.VkCode);
    return true;
  }

  private static bool IsDown(VirtualKey key) => (NativeMethods.GetAsyncKeyState((int)key) & 0x8000) != 0;
}
