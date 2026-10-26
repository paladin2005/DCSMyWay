using System.ComponentModel;
using DCSMyWay.Models;
using Microsoft.UI.Dispatching;

namespace DCSMyWay.Services;

public sealed class ShortcutService(SettingsService settings) : IDisposable
{
  private KeyboardHook? _hook;
  private DispatcherQueue? _dispatcher;

  public event EventHandler<ShortcutAction>? Triggered;

  public Action<Shortcut>? Capture { get; set; }

  public bool TryStart()
  {
    _dispatcher = DispatcherQueue.GetForCurrentThread();
    _hook = new KeyboardHook(OnKeyPressed);
    try
    {
      _hook.Start();
      return true;
    }
    catch (Win32Exception)
    {
      return false;
    }
  }

  public void Dispose() => _hook?.Dispose();

  private bool OnKeyPressed(KeyPress press)
  {
    var shortcut = Shortcut.From(press);
    if (Capture is { } capture)
    {
      _dispatcher?.TryEnqueue(() => capture(shortcut));
      return true;
    }

    if (settings.Current.ActionFor(shortcut) is not { } action) return false;
    _dispatcher?.TryEnqueue(() => Triggered?.Invoke(this, action));
    return true;
  }
}
