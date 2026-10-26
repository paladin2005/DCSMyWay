namespace DCSMyWay.Models;

public sealed record AppSettings(
  bool AlwaysOnTop = true,
  bool DontTakeFocus = true,
  AppTheme Theme = AppTheme.System,
  Shortcut? CrosshairShortcut = null,
  Shortcut? AddWaypointShortcut = null,
  Shortcut? DeleteLastWaypointShortcut = null,
  Shortcut? TransferShortcut = null,
  Shortcut? ClearAllShortcut = null)
{
  public Shortcut? ShortcutFor(ShortcutAction action) => action switch
  {
    ShortcutAction.ToggleCrosshair => CrosshairShortcut,
    ShortcutAction.AddWaypoint => AddWaypointShortcut,
    ShortcutAction.DeleteLastWaypoint => DeleteLastWaypointShortcut,
    ShortcutAction.Transfer => TransferShortcut,
    ShortcutAction.ClearAll => ClearAllShortcut,
    _ => null,
  };

  public AppSettings WithShortcut(ShortcutAction action, Shortcut? shortcut) => action switch
  {
    ShortcutAction.ToggleCrosshair => this with { CrosshairShortcut = shortcut },
    ShortcutAction.AddWaypoint => this with { AddWaypointShortcut = shortcut },
    ShortcutAction.DeleteLastWaypoint => this with { DeleteLastWaypointShortcut = shortcut },
    ShortcutAction.Transfer => this with { TransferShortcut = shortcut },
    ShortcutAction.ClearAll => this with { ClearAllShortcut = shortcut },
    _ => this,
  };

  public ShortcutAction? ActionFor(Shortcut shortcut) =>
    Enum.GetValues<ShortcutAction>().Where(a => ShortcutFor(a) == shortcut).Select(a => (ShortcutAction?)a).FirstOrDefault();
}
