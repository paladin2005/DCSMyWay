using System.Runtime.InteropServices;
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

    ExtendsContentIntoTitleBar = true;
    SetTitleBar(AppTitleBar);
    AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

    var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
    var scale = GetDpiForWindow(hwnd) / 96.0;
    AppWindow.Resize(new SizeInt32((int)(WindowWidth * scale), (int)(WindowHeight * scale)));
    if (AppWindow.Presenter is OverlappedPresenter presenter)
    {
      presenter.IsResizable = false;
      presenter.IsMaximizable = false;
    }

    RootFrame.Navigate(typeof(MainPage));
  }

  [DllImport("user32.dll")]
  private static extern uint GetDpiForWindow(IntPtr hwnd);
}
