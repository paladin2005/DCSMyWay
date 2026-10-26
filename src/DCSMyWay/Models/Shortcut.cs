using System.Text.Json.Serialization;
using Windows.System;

namespace DCSMyWay.Models;

public sealed record Shortcut(
  [property: JsonConverter(typeof(JsonStringEnumConverter<VirtualKey>))] VirtualKey Key,
  bool Control = false,
  bool Shift = false,
  bool Alt = false)
{
  public static Shortcut From(KeyPress press) => new(press.Key, press.Control, press.Shift, press.Alt);

  public bool Matches(KeyPress press) => press.Key == Key && press.Control == Control && press.Shift == Shift && press.Alt == Alt;

  public override string ToString() => (Control ? "Ctrl+" : "") + (Shift ? "Shift+" : "") + (Alt ? "Alt+" : "") + KeyName(Key);

  private static string KeyName(VirtualKey key) => key switch
  {
    >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((int)key - (int)VirtualKey.Number0).ToString(),
    >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9 => $"Num {(int)key - (int)VirtualKey.NumberPad0}",
    VirtualKey.Multiply => "Num *",
    VirtualKey.Add => "Num +",
    VirtualKey.Subtract => "Num -",
    VirtualKey.Decimal => "Num .",
    VirtualKey.Divide => "Num /",
    VirtualKey.PageUp => "Page Up",
    VirtualKey.PageDown => "Page Down",
    VirtualKey.Back => "Backspace",
    VirtualKey.Escape => "Esc",
    (VirtualKey)186 => ";",
    (VirtualKey)187 => "=",
    (VirtualKey)188 => ",",
    (VirtualKey)189 => "-",
    (VirtualKey)190 => ".",
    (VirtualKey)191 => "/",
    (VirtualKey)192 => "`",
    (VirtualKey)219 => "[",
    (VirtualKey)220 => "\\",
    (VirtualKey)221 => "]",
    (VirtualKey)222 => "'",
    _ => Enum.IsDefined(key) ? key.ToString() : $"Key {(int)key}",
  };
}
