using DCSMyWay.Interop;
using DCSMyWay.Models;
using DCSMyWay.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DCSMyWay;

public sealed partial class MainPage : Page
{
  private const int MaxKeyLogLines = 6;
  private readonly KeyboardHook _keyboardHook = new();
  private readonly List<string> _keyLog = [];
  private DispatcherQueueTimer? _foregroundTimer;
  private nint _focusReturnWindow;
  private int _clickCount;

  public MainPage()
  {
    InitializeComponent();
    Loaded += OnLoaded;
    Unloaded += OnUnloaded;
  }

  private void OnLoaded(object sender, RoutedEventArgs e)
  {
    _keyboardHook.KeyPressed += OnKeyPressed;
    _keyboardHook.Start();

    _foregroundTimer = DispatcherQueue.CreateTimer();
    _foregroundTimer.Interval = TimeSpan.FromMilliseconds(250);
    _foregroundTimer.Tick += (_, _) => ForegroundText.Text = $"Foreground: {GetForegroundTitle()}";
    _foregroundTimer.Start();
  }

  private void OnUnloaded(object sender, RoutedEventArgs e)
  {
    _foregroundTimer?.Stop();
    _keyboardHook.Dispose();
  }

  private void OnKeyPressed(object? sender, KeyPress keyPress)
  {
    DispatcherQueue.TryEnqueue(() =>
    {
      _keyLog.Insert(0, $"{keyPress} -> {GetForegroundTitle()}");
      if (_keyLog.Count > MaxKeyLogLines) _keyLog.RemoveAt(MaxKeyLogLines);
      KeyLogText.Text = string.Join("\n", _keyLog);
    });
  }

  private void ClickTestButton_Click(object sender, RoutedEventArgs e)
  {
    _clickCount++;
    ClickCountText.Text = $"Clicks: {_clickCount}";
  }

  private void TypingTestBox_PointerEntered(object sender, PointerRoutedEventArgs e)
  {
    if (!App.Settings.Current.DontTakeFocus) return;
    var foreground = NativeMethods.GetForegroundWindow();
    if (foreground != App.MainWindow.Hwnd) _focusReturnWindow = foreground;
    App.MainWindow.SetActivatable(true);
  }

  private void TypingTestBox_PointerExited(object sender, PointerRoutedEventArgs e)
  {
    if (!App.Settings.Current.DontTakeFocus) return;
    App.MainWindow.SetActivatable(false);
    if (App.MainWindow.IsForeground && _focusReturnWindow != 0) NativeMethods.SetForegroundWindow(_focusReturnWindow);
  }

  private static string GetForegroundTitle()
  {
    var hwnd = NativeMethods.GetForegroundWindow();
    if (hwnd == App.MainWindow.Hwnd) return "DCSMyWay";
    var buffer = new char[256];
    var length = NativeMethods.GetWindowText(hwnd, buffer, buffer.Length);
    return length > 0 ? new string(buffer, 0, length) : "(none)";
  }
}
