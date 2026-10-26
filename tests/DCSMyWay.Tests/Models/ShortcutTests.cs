using DCSMyWay.Models;
using Windows.System;

namespace DCSMyWay.Tests.Models;

public class ShortcutTests
{
  [Fact]
  public void MatchesOnlyTheSameKeyAndModifiers()
  {
    var shortcut = new Shortcut(VirtualKey.T, Control: true, Shift: true);

    Assert.True(shortcut.Matches(new KeyPress(VirtualKey.T, true, true, false, false)));
    Assert.True(shortcut.Matches(new KeyPress(VirtualKey.T, true, true, false, true)));
    Assert.False(shortcut.Matches(new KeyPress(VirtualKey.T, true, false, false, false)));
    Assert.False(shortcut.Matches(new KeyPress(VirtualKey.T, true, true, true, false)));
    Assert.False(shortcut.Matches(new KeyPress(VirtualKey.Y, true, true, false, false)));
  }

  [Fact]
  public void IsBuiltFromAKeyPressWithoutTheInjectedFlag()
  {
    Assert.Equal(new Shortcut(VirtualKey.F5, Alt: true), Shortcut.From(new KeyPress(VirtualKey.F5, false, false, true, true)));
  }

  [Theory]
  [InlineData(VirtualKey.A, true, true, true, "Ctrl+Shift+Alt+A")]
  [InlineData(VirtualKey.Number7, false, false, false, "7")]
  [InlineData(VirtualKey.NumberPad3, true, false, false, "Ctrl+Num 3")]
  [InlineData(VirtualKey.F12, false, true, false, "Shift+F12")]
  [InlineData(VirtualKey.PageDown, false, false, false, "Page Down")]
  [InlineData((VirtualKey)189, false, false, true, "Alt+-")]
  [InlineData((VirtualKey)255, false, false, false, "Key 255")]
  public void ShowsTheKeysLikeWindows(VirtualKey key, bool control, bool shift, bool alt, string text)
  {
    Assert.Equal(text, new Shortcut(key, control, shift, alt).ToString());
  }

  [Fact]
  public void SettingsKeepOneShortcutPerAction()
  {
    var transfer = new Shortcut(VirtualKey.T, Control: true);
    var settings = new AppSettings().WithShortcut(ShortcutAction.Transfer, transfer);

    Assert.Equal(transfer, settings.TransferShortcut);
    Assert.Equal(transfer, settings.ShortcutFor(ShortcutAction.Transfer));
    Assert.Null(settings.ShortcutFor(ShortcutAction.ClearAll));
    Assert.Null(settings.WithShortcut(ShortcutAction.Transfer, null).TransferShortcut);
  }

  [Fact]
  public void FindsTheActionOfAShortcut()
  {
    var settings = new AppSettings()
      .WithShortcut(ShortcutAction.ToggleCrosshair, new Shortcut(VirtualKey.C, Control: true))
      .WithShortcut(ShortcutAction.AddWaypoint, new Shortcut(VirtualKey.A, Control: true))
      .WithShortcut(ShortcutAction.DeleteLastWaypoint, new Shortcut(VirtualKey.Back, Control: true))
      .WithShortcut(ShortcutAction.Transfer, new Shortcut(VirtualKey.T, Control: true))
      .WithShortcut(ShortcutAction.ClearAll, new Shortcut(VirtualKey.Delete, Control: true));

    Assert.Equal(ShortcutAction.ToggleCrosshair, settings.ActionFor(new Shortcut(VirtualKey.C, Control: true)));
    Assert.Equal(ShortcutAction.AddWaypoint, settings.ActionFor(new Shortcut(VirtualKey.A, Control: true)));
    Assert.Equal(ShortcutAction.DeleteLastWaypoint, settings.ActionFor(new Shortcut(VirtualKey.Back, Control: true)));
    Assert.Equal(ShortcutAction.Transfer, settings.ActionFor(new Shortcut(VirtualKey.T, Control: true)));
    Assert.Equal(ShortcutAction.ClearAll, settings.ActionFor(new Shortcut(VirtualKey.Delete, Control: true)));
    Assert.Null(settings.ActionFor(new Shortcut(VirtualKey.T)));
  }

  [Fact]
  public void HasNoShortcutsByDefault()
  {
    Assert.All(Enum.GetValues<ShortcutAction>(), action => Assert.Null(new AppSettings().ShortcutFor(action)));
  }
}
