namespace DCSMyWay.Models;

public sealed record AppSettings(
  bool AlwaysOnTop = true,
  bool DontTakeFocus = true,
  bool StartAtActiveSteerpoint = true,
  AppTheme Theme = AppTheme.System);
