using DCSMyWay.Models;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace DCSMyWay.Interop;

internal static class WindowInterop
{
  public static nint GetHwnd(Window window) => Win32Interop.GetWindowFromWindowId(window.AppWindow.Id);

  public static void Resize(Window window, int width, int height)
  {
    var scale = NativeMethods.GetDpiForWindow(GetHwnd(window)) / 96.0;
    window.AppWindow.Resize(new SizeInt32((int)(width * scale), (int)(height * scale)));
  }

  public static void SetActivatable(Window window, bool activatable)
  {
    var hwnd = GetHwnd(window);
    var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle);
    style = activatable ? style & ~NativeMethods.WsExNoActivate : style | NativeMethods.WsExNoActivate;
    NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, style);
  }

  public static void SetTheme(Window window, AppTheme theme)
  {
    if (window.Content is FrameworkElement root)
    {
      root.RequestedTheme = theme switch
      {
        AppTheme.Light => ElementTheme.Light,
        AppTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
      };
    }

    window.AppWindow.TitleBar.PreferredTheme = theme switch
    {
      AppTheme.Light => TitleBarTheme.Light,
      AppTheme.Dark => TitleBarTheme.Dark,
      _ => TitleBarTheme.UseDefaultAppMode,
    };
  }

  public static void SetOwner(Window window, Window owner) =>
    NativeMethods.SetWindowLongPtr(GetHwnd(window), NativeMethods.GwlpHwndParent, GetHwnd(owner));
}
