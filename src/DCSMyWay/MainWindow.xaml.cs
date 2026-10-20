using DCSMyWay.Interop;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace DCSMyWay;

public sealed partial class MainWindow : Window
{
  private const int WindowWidth = 360;
  private const int WindowHeight = 520;

  public MainWindow()
  {
    InitializeComponent();

    Hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
    ExtendsContentIntoTitleBar = true;
    SetTitleBar(AppTitleBar);
    AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

    var scale = NativeMethods.GetDpiForWindow(Hwnd) / 96.0;
    AppWindow.Resize(new SizeInt32((int)(WindowWidth * scale), (int)(WindowHeight * scale)));
    if (AppWindow.Presenter is OverlappedPresenter presenter)
    {
      presenter.IsResizable = false;
      presenter.IsMaximizable = false;
    }
    SetAlwaysOnTop(true);
    SetActivatable(false);

    RootFrame.Navigate(typeof(MainPage));
  }

  public nint Hwnd { get; }

  public bool IsForeground => NativeMethods.GetForegroundWindow() == Hwnd;

  public void SetAlwaysOnTop(bool alwaysOnTop)
  {
    if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = alwaysOnTop;
  }

  public void SetActivatable(bool activatable)
  {
    var style = NativeMethods.GetWindowLongPtr(Hwnd, NativeMethods.GwlExStyle);
    style = activatable ? style & ~NativeMethods.WsExNoActivate : style | NativeMethods.WsExNoActivate;
    NativeMethods.SetWindowLongPtr(Hwnd, NativeMethods.GwlExStyle, style);
  }
}
