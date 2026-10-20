using System.Text.Json;
using DCSMyWay.Models;

namespace DCSMyWay.Services;

public sealed class SettingsService(string settingsPath)
{
  public static readonly string DefaultPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DCSMyWay", "settings.json");

  private readonly SemaphoreSlim _saveLock = new(1, 1);

  public AppSettings Current { get; private set; } = new();

  public event EventHandler<AppSettings>? Changed;

  public async Task LoadAsync()
  {
    if (!File.Exists(settingsPath)) return;
    try
    {
      await using var stream = File.OpenRead(settingsPath);
      Current = await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.AppSettings) ?? new();
    }
    catch (JsonException)
    {
      Current = new();
    }
  }

  public async Task UpdateAsync(Func<AppSettings, AppSettings> update)
  {
    Current = update(Current);
    Changed?.Invoke(this, Current);

    await _saveLock.WaitAsync();
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
      await using var stream = File.Create(settingsPath);
      await JsonSerializer.SerializeAsync(stream, Current, AppJsonContext.Default.AppSettings);
    }
    finally
    {
      _saveLock.Release();
    }
  }
}
