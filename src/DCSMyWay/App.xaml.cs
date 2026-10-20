using Microsoft.UI.Xaml;

namespace DCSMyWay;

public partial class App : Application
{
  private Window? _window;

  public App()
  {
    InitializeComponent();
    UnhandledException += (_, e) => File.WriteAllText(
      Path.Combine(Path.GetTempPath(), "DCSMyWay-crash.log"),
      $"{e.Message}\n{e.Exception}");
  }

  protected override void OnLaunched(LaunchActivatedEventArgs args)
  {
    _window = new MainWindow();
    _window.Activate();
  }
}
