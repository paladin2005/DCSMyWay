using DCSMyWay.Services;
using Microsoft.UI.Xaml;

namespace DCSMyWay;

public partial class App : Application
{
  public App()
  {
    InitializeComponent();
    UnhandledException += (_, e) => File.WriteAllText(
      Path.Combine(Path.GetTempPath(), "DCSMyWay-crash.log"),
      $"{e.Message}\n{e.Exception}");
  }

  public static SettingsService Settings { get; } = new(SettingsService.DefaultPath);

  public static DcsListener DcsListener { get; } = new(DcsListener.DefaultPort);

  public static DcsSender DcsSender { get; } = new(DcsSender.DefaultHookPort, DcsSender.DefaultCockpitPort);

  public static MainWindow MainWindow { get; private set; } = null!;

  protected override async void OnLaunched(LaunchActivatedEventArgs args)
  {
    await Settings.LoadAsync();
    DcsListener.TryStart();
    MainWindow = new MainWindow();
    MainWindow.Closed += (_, _) => Exit();
    MainWindow.Activate();
  }
}
