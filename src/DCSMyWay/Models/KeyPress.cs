using Windows.System;

namespace DCSMyWay.Models;

public sealed record KeyPress(VirtualKey Key, bool Control, bool Shift, bool Alt, bool Injected)
{
  public override string ToString()
  {
    var modifiers = (Control ? "Ctrl+" : "") + (Shift ? "Shift+" : "") + (Alt ? "Alt+" : "");
    return $"{modifiers}{Key}{(Injected ? " (injected)" : "")}";
  }
}
