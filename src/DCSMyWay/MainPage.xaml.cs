using System.Collections.ObjectModel;
using DCSMyWay.Aircraft;
using DCSMyWay.Models;
using DCSMyWay.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DCSMyWay;

public sealed partial class MainPage : Page
{
  private static readonly TimeSpan CrosshairSyncDelay = TimeSpan.FromSeconds(1);

  private readonly ObservableCollection<Waypoint> _waypoints = [];
  private DispatcherQueueTimer? _refreshTimer;
  private DateTimeOffset _crosshairClickedUtc;
  private int _nextWaypointNumber = 1;
  private bool _wasConnected;

  public MainPage()
  {
    InitializeComponent();
    WaypointList.ItemsSource = _waypoints;
    Loaded += OnLoaded;
    Unloaded += OnUnloaded;
  }

  public static string Describe(double latitude, double longitude, double elevationMeters)
  {
    var cockpit = F16.Format(new Waypoint(0, "", latitude, longitude, elevationMeters));
    return $"{cockpit.LatitudeHemisphere} {cockpit.Latitude}   {cockpit.LongitudeHemisphere} {cockpit.Longitude}   {cockpit.ElevationFeet} ft";
  }

  private void OnLoaded(object sender, RoutedEventArgs e)
  {
    App.DcsListener.MarkReceived += OnMarkReceived;
    _refreshTimer = DispatcherQueue.CreateTimer();
    _refreshTimer.Interval = TimeSpan.FromMilliseconds(250);
    _refreshTimer.Tick += async (_, _) => await OnRefreshTickAsync();
    _refreshTimer.Start();
    Refresh();
  }

  private void OnUnloaded(object sender, RoutedEventArgs e)
  {
    App.DcsListener.MarkReceived -= OnMarkReceived;
    _refreshTimer?.Stop();
  }

  private async Task OnRefreshTickAsync()
  {
    Refresh();
    var connected = App.DcsListener.IsConnected;
    var reconnected = connected && !_wasConnected;
    _wasConnected = connected;
    if (reconnected && _waypoints.Count > 0) await App.DcsSender.ShowRouteAsync(_waypoints);
  }

  private void Refresh()
  {
    var state = App.DcsListener.IsConnected ? App.DcsListener.Latest : null;
    DcsStatusText.Text = GetDcsStatus(state);
    CrosshairButton.IsEnabled = state is not null;
    if (DateTimeOffset.UtcNow - _crosshairClickedUtc > CrosshairSyncDelay) CrosshairButton.IsChecked = state?.CrosshairVisible == true;
    TransferButton.IsEnabled = _waypoints.Count > 0 && state is { IsEnteringWaypoints: false };
    ClearButton.IsEnabled = _waypoints.Count > 0;
  }

  private void OnMarkReceived(object? sender, DcsPosition position) =>
    DispatcherQueue.TryEnqueue(async () => await AddWaypointAsync(position));

  private async Task AddWaypointAsync(DcsPosition position)
  {
    var number = _nextWaypointNumber++;
    var waypoint = new Waypoint(number, $"W{number}", position.Latitude, position.Longitude, position.ElevationMeters);
    _waypoints.Add(waypoint);
    WaypointList.ScrollIntoView(waypoint);
    Refresh();
    await App.DcsSender.ShowStatusAsync($"{waypoint.Name} added");
    await App.DcsSender.ShowRouteAsync(_waypoints);
  }

  private async void CrosshairButton_Click(object sender, RoutedEventArgs e)
  {
    _crosshairClickedUtc = DateTimeOffset.UtcNow;
    var visible = CrosshairButton.IsChecked == true;
    if (await App.DcsSender.SetCrosshairVisibleAsync(visible)) return;
    CrosshairButton.IsChecked = !visible;
    ShowMessage(InfoBarSeverity.Error, "DCS is not reachable");
  }

  private async void TransferButton_Click(object sender, RoutedEventArgs e)
  {
    var module = App.DcsListener.Latest?.Position.Module;
    if (module is null || !F16.ModuleNames.Contains(module))
    {
      ShowMessage(InfoBarSeverity.Warning, $"Only the F-16 is supported for now ({module ?? "no aircraft"})");
      return;
    }

    var presses = F16.CreateButtonPresses(_waypoints.Select(F16.Format), App.Settings.Current.StartAtActiveSteerpoint);
    var sent = await App.DcsSender.SendWaypointsAsync(presses);
    if (sent) ShowMessage(InfoBarSeverity.Success, $"Entering {_waypoints.Count} waypoints, don't touch the cockpit controls");
    else ShowMessage(InfoBarSeverity.Error, "DCS is not reachable");
  }

  private async void DeleteWaypointButton_Click(object sender, RoutedEventArgs e)
  {
    if (((FrameworkElement)sender).DataContext is not Waypoint waypoint) return;
    _waypoints.Remove(waypoint);
    Refresh();
    await App.DcsSender.ShowRouteAsync(_waypoints);
  }

  private async void ClearButton_Click(object sender, RoutedEventArgs e)
  {
    _waypoints.Clear();
    _nextWaypointNumber = 1;
    Refresh();
    await App.DcsSender.ShowRouteAsync(_waypoints);
  }

  private void ShowMessage(InfoBarSeverity severity, string message)
  {
    MessageBar.Severity = severity;
    MessageBar.Message = message;
    MessageBar.IsOpen = true;
  }

  private static string GetDcsStatus(DcsState? state)
  {
    if (!App.DcsListener.IsListening) return $"Port {DcsListener.DefaultPort} is in use by another program. Close it and restart DCSMyWay.";
    if (state is null) return "No connection to DCS";
    var camera = Describe(state.Position.Latitude, state.Position.Longitude, state.Position.ElevationMeters);
    var activity = state.IsEnteringWaypoints ? "\nEntering waypoints..." : "";
    return $"{state.Position.Module}\n{camera}{activity}";
  }
}
