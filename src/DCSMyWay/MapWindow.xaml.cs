using DCSMyWay.Interop;
using DCSMyWay.Services;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace DCSMyWay;

public sealed partial class MapWindow : Window
{
  private const string Host = "map.dcsmyway";
  private const int WindowWidth = 960;
  private const int WindowHeight = 720;

  private static readonly string UserDataFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DCSMyWay", "WebView2");

  private string? _state;
  private bool _ready;

  public MapWindow()
  {
    InitializeComponent();

    ExtendsContentIntoTitleBar = true;
    SetTitleBar(AppTitleBar);
    WindowInterop.Resize(this, WindowWidth, WindowHeight);
    MapView.Loaded += async (_, _) => await InitializeMapAsync();
    Closed += (_, _) => MapView.Close();
  }

  public void Update(MapState state)
  {
    var json = state.ToJson();
    if (json == _state) return;
    _state = json;
    if (_ready) MapView.CoreWebView2.PostWebMessageAsJson(json);
  }

  private async Task InitializeMapAsync()
  {
    try
    {
      var options = new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = "--disable-gpu --renderer-process-limit=1" };
      var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, UserDataFolder, options);
      await MapView.EnsureCoreWebView2Async(environment);
      var webView = MapView.CoreWebView2;
      webView.Settings.AreDefaultContextMenusEnabled = false;
      webView.Settings.AreDevToolsEnabled = false;
      webView.SetVirtualHostNameToFolderMapping(Host, Path.Combine(AppContext.BaseDirectory, "Assets", "Map"), CoreWebView2HostResourceAccessKind.DenyCors);
      webView.NavigationCompleted += (_, args) =>
      {
        if (!args.IsSuccess) return;
        _ready = true;
        if (_state is not null) webView.PostWebMessageAsJson(_state);
      };
      webView.Navigate($"https://{Host}/index.html");
    }
    catch (Exception e)
    {
      ErrorBar.Message = e.Message;
      ErrorBar.IsOpen = true;
    }
  }
}
