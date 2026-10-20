using DCSMyWay.Models;
using DCSMyWay.Services;

namespace DCSMyWay.Tests.Services;

public sealed class SettingsServiceTests : IDisposable
{
  private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DCSMyWay-tests-{Guid.NewGuid()}");

  private string SettingsPath => Path.Combine(_directory, "settings.json");

  public void Dispose()
  {
    if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
  }

  [Fact]
  public async Task UsesDefaultsWhenThereIsNoSettingsFile()
  {
    var service = new SettingsService(SettingsPath);

    await service.LoadAsync();

    Assert.Equal(new AppSettings(), service.Current);
  }

  [Fact]
  public async Task PersistsUpdatesAcrossRestarts()
  {
    var service = new SettingsService(SettingsPath);
    await service.UpdateAsync(s => s with { AlwaysOnTop = false, DontTakeFocus = false });

    var restarted = new SettingsService(SettingsPath);
    await restarted.LoadAsync();

    Assert.Equal(new AppSettings(AlwaysOnTop: false, DontTakeFocus: false), restarted.Current);
  }

  [Fact]
  public async Task RaisesChangedWithTheNewSettings()
  {
    var service = new SettingsService(SettingsPath);
    AppSettings? notified = null;
    service.Changed += (_, settings) => notified = settings;

    await service.UpdateAsync(s => s with { AlwaysOnTop = false });

    Assert.Equal(new AppSettings(AlwaysOnTop: false), notified);
  }

  [Fact]
  public async Task FallsBackToDefaultsWhenTheFileIsCorrupted()
  {
    Directory.CreateDirectory(_directory);
    await File.WriteAllTextAsync(SettingsPath, "{ not json", TestContext.Current.CancellationToken);
    var service = new SettingsService(SettingsPath);

    await service.LoadAsync();

    Assert.Equal(new AppSettings(), service.Current);
  }

  [Fact]
  public async Task KeepsDefaultsForSettingsMissingFromOlderFiles()
  {
    Directory.CreateDirectory(_directory);
    await File.WriteAllTextAsync(SettingsPath, """{ "AlwaysOnTop": false }""", TestContext.Current.CancellationToken);
    var service = new SettingsService(SettingsPath);

    await service.LoadAsync();

    Assert.Equal(new AppSettings(AlwaysOnTop: false, DontTakeFocus: true), service.Current);
  }
}
