using DCSMyWay.Interop;
using DCSMyWay.Models;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DCSMyWay;

public sealed partial class MainWindow : Window
{
  private const int WindowWidth = 360;
  private const int WindowHeight = 520;
  private SettingsWindow? _settingsWindow;

  public MainWindow()
  {
    InitializeComponent();

    Hwnd = WindowInterop.GetHwnd(this);
    ExtendsContentIntoTitleBar = true;
    SetTitleBar(AppTitleBar);
    AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
    WindowInterop.Resize(this, WindowWidth, WindowHeight);
    if (AppWindow.Presenter is OverlappedPresenter presenter)
    {
      presenter.IsResizable = false;
      presenter.IsMaximizable = false;
    }

    ApplySettings(App.Settings.Current);
    App.Settings.Changed += (_, settings) => ApplySettings(settings);

    RootFrame.Navigate(typeof(MainPage));
  }

  public nint Hwnd { get; }

  public bool IsForeground => NativeMethods.GetForegroundWindow() == Hwnd;

  public void SetActivatable(bool activatable) => WindowInterop.SetActivatable(this, activatable);

  public async Task<ContentDialogResult> ShowWithKeyboardAsync(ContentDialog dialog)
  {
    var previous = NativeMethods.GetForegroundWindow();
    SetActivatable(true);
    NativeMethods.SetForegroundWindow(Hwnd);
    try
    {
      return await dialog.ShowAsync();
    }
    finally
    {
      var dontTakeFocus = App.Settings.Current.DontTakeFocus;
      SetActivatable(!dontTakeFocus);
      if (dontTakeFocus && previous != Hwnd) NativeMethods.SetForegroundWindow(previous);
    }
  }

  private void ApplySettings(AppSettings settings)
  {
    if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = settings.AlwaysOnTop;
    SetActivatable(!settings.DontTakeFocus);
    WindowInterop.SetTheme(this, settings.Theme);
  }

  private void SettingsButton_Click(object sender, RoutedEventArgs e)
  {
    if (_settingsWindow is null)
    {
      _settingsWindow = new SettingsWindow(this);
      _settingsWindow.Closed += (_, _) => _settingsWindow = null;
    }
    _settingsWindow.Show();
  }
}
