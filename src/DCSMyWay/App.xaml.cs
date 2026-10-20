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

  public static MainWindow MainWindow { get; private set; } = null!;

  protected override void OnLaunched(LaunchActivatedEventArgs args)
  {
    MainWindow = new MainWindow();
    MainWindow.Activate();
  }
}
