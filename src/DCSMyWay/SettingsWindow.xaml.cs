using DCSMyWay.Interop;
using DCSMyWay.Models;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace DCSMyWay;

public sealed partial class SettingsWindow : Window
{
  private const int WindowWidth = 320;
  private const int WindowHeight = 380;

  public SettingsWindow(Window owner)
  {
    InitializeComponent();

    ExtendsContentIntoTitleBar = true;
    SetTitleBar(AppTitleBar);
    AppWindow.IsShownInSwitchers = false;
    WindowInterop.SetOwner(this, owner);
    WindowInterop.Resize(this, WindowWidth, WindowHeight);
    if (AppWindow.Presenter is OverlappedPresenter presenter)
    {
      presenter.IsResizable = false;
      presenter.IsMaximizable = false;
      presenter.IsMinimizable = false;
      presenter.IsModal = true;
    }
    CenterOn(owner);
    ApplySettings(App.Settings.Current);

    var settings = App.Settings.Current;
    AlwaysOnTopSwitch.IsOn = settings.AlwaysOnTop;
    DontTakeFocusSwitch.IsOn = settings.DontTakeFocus;
    StartAtActiveSteerpointSwitch.IsOn = settings.StartAtActiveSteerpoint;
    ThemeButtons.SelectedIndex = (int)settings.Theme;
    AlwaysOnTopSwitch.Toggled += async (_, _) =>
      await App.Settings.UpdateAsync(s => s with { AlwaysOnTop = AlwaysOnTopSwitch.IsOn });
    DontTakeFocusSwitch.Toggled += async (_, _) =>
      await App.Settings.UpdateAsync(s => s with { DontTakeFocus = DontTakeFocusSwitch.IsOn });
    StartAtActiveSteerpointSwitch.Toggled += async (_, _) =>
      await App.Settings.UpdateAsync(s => s with { StartAtActiveSteerpoint = StartAtActiveSteerpointSwitch.IsOn });
    ThemeButtons.SelectionChanged += async (_, _) =>
      await App.Settings.UpdateAsync(s => s with { Theme = (AppTheme)ThemeButtons.SelectedIndex });

    App.Settings.Changed += OnSettingsChanged;
    Closed += (_, _) => App.Settings.Changed -= OnSettingsChanged;
  }

  public void Show()
  {
    if (App.Settings.Current.DontTakeFocus) AppWindow.Show(false);
    else Activate();
  }

  private void OnSettingsChanged(object? sender, AppSettings settings) => ApplySettings(settings);

  private void ApplySettings(AppSettings settings)
  {
    if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = settings.AlwaysOnTop;
    WindowInterop.SetActivatable(this, !settings.DontTakeFocus);
    WindowInterop.SetTheme(this, settings.Theme);
  }

  private void CenterOn(Window owner)
  {
    var ownerPosition = owner.AppWindow.Position;
    var ownerSize = owner.AppWindow.Size;
    var size = AppWindow.Size;
    AppWindow.Move(new PointInt32(
      ownerPosition.X + (ownerSize.Width - size.Width) / 2,
      ownerPosition.Y + (ownerSize.Height - size.Height) / 2));
  }
}
