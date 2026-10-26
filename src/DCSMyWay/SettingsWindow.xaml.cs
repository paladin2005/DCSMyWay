using DCSMyWay.Interop;
using DCSMyWay.Models;
using DCSMyWay.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace DCSMyWay;

public sealed partial class SettingsWindow : Window
{
  private const int WindowWidth = 360;
  private const int WindowHeight = 640;

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
    ThemeButtons.SelectedIndex = (int)settings.Theme;
    ShowShortcuts(settings);
    AlwaysOnTopSwitch.Toggled += async (_, _) =>
      await App.Settings.UpdateAsync(s => s with { AlwaysOnTop = AlwaysOnTopSwitch.IsOn });
    DontTakeFocusSwitch.Toggled += async (_, _) =>
      await App.Settings.UpdateAsync(s => s with { DontTakeFocus = DontTakeFocusSwitch.IsOn });
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

  private Button[] ShortcutButtons =>
    [ToggleCrosshairShortcutButton, AddWaypointShortcutButton, DeleteLastWaypointShortcutButton, TransferShortcutButton, ClearAllShortcutButton];

  private void OnSettingsChanged(object? sender, AppSettings settings)
  {
    ApplySettings(settings);
    ShowShortcuts(settings);
  }

  private void ShowShortcuts(AppSettings settings)
  {
    foreach (var button in ShortcutButtons) button.Content = settings.ShortcutFor(ActionOf(button))?.ToString() ?? "None";
  }

  private static ShortcutAction ActionOf(Button button) => Enum.Parse<ShortcutAction>((string)button.Tag);

  private static string ShortcutName(ShortcutAction action) => action switch
  {
    ShortcutAction.ToggleCrosshair => "Show/hide crosshair",
    ShortcutAction.AddWaypoint => "Add waypoint",
    ShortcutAction.DeleteLastWaypoint => "Delete last waypoint",
    ShortcutAction.Transfer => "Transfer or cancel",
    _ => "Clear all",
  };

  private async void ShortcutButton_Click(object sender, RoutedEventArgs e)
  {
    var action = ActionOf((Button)sender);
    Shortcut? captured = null;
    ShortcutDialog.Title = ShortcutName(action);
    ShortcutDialog.IsSecondaryButtonEnabled = App.Settings.Current.ShortcutFor(action) is not null;
    ShowCapturedShortcut(action, null);
    App.Shortcuts.Capture = shortcut =>
    {
      captured = shortcut;
      ShowCapturedShortcut(action, shortcut);
    };

    ContentDialogResult result;
    try
    {
      result = await ShortcutDialog.ShowAsync();
    }
    finally
    {
      App.Shortcuts.Capture = null;
    }

    if (result == ContentDialogResult.Primary && captured is not null) await App.Settings.UpdateAsync(s => s.WithShortcut(action, captured));
    else if (result == ContentDialogResult.Secondary) await App.Settings.UpdateAsync(s => s.WithShortcut(action, null));
  }

  private void ShowCapturedShortcut(ShortcutAction action, Shortcut? shortcut)
  {
    var owner = shortcut is null ? null : App.Settings.Current.ActionFor(shortcut);
    var conflict = owner is not null && owner != action;
    CapturedShortcutText.Text = shortcut?.ToString() ?? "Waiting for keys";
    ShortcutConflictBar.Message = conflict ? $"Already used by {ShortcutName(owner!.Value)}" : "";
    ShortcutConflictBar.IsOpen = conflict;
    ShortcutDialog.IsPrimaryButtonEnabled = shortcut is not null && !conflict;
  }

  private async void RemoveFromDcsButton_Click(object sender, RoutedEventArgs e)
  {
    if (await RemoveFromDcsDialog.ShowAsync() != ContentDialogResult.Primary) return;
    try
    {
      foreach (var folder in await DcsFolders.FindAsync()) await App.DcsScripts.RemoveAsync(folder);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      MessageBar.Message = $"DCSMyWay could not be removed from DCS: {exception.Message}";
      MessageBar.IsOpen = true;
      return;
    }
    Application.Current.Exit();
  }

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
