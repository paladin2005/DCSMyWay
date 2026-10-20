using Microsoft.UI;
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

  public static void SetOwner(Window window, Window owner) =>
    NativeMethods.SetWindowLongPtr(GetHwnd(window), NativeMethods.GwlpHwndParent, GetHwnd(owner));
}
