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
  private static readonly TimeSpan DcsSyncDelay = TimeSpan.FromSeconds(1);
  private static readonly TimeSpan TransferStartTimeout = TimeSpan.FromSeconds(3);

  private readonly ObservableCollection<WaypointItem> _items = [];
  private readonly TransferHistory _transferHistory = new();
  private DispatcherQueueTimer? _refreshTimer;
  private DateTimeOffset _crosshairClickedUtc;
  private DateTimeOffset _transferClickedUtc;
  private string? _transferMessage;
  private bool _transferStarted;
  private int _nextWaypointId = 1;
  private int _nextWaypointNumber = 1;
  private bool _wasConnected;

  public MainPage()
  {
    InitializeComponent();
    WaypointList.ItemsSource = _items;
    Loaded += OnLoaded;
    Unloaded += OnUnloaded;
  }

  private List<Waypoint> Waypoints => [.. _items.Select(i => i.Waypoint)];

  private bool IsTransferInProgress => _transferMessage is not null;

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
    if (!connected) _transferHistory.Clear();
    if (reconnected && _items.Count > 0) await App.DcsSender.ShowRouteAsync(Waypoints);
  }

  private void Refresh()
  {
    var state = App.DcsListener.IsConnected ? App.DcsListener.Latest : null;
    ShowDcsState(state);
    CrosshairButton.IsEnabled = state is not null;
    if (DateTimeOffset.UtcNow - _crosshairClickedUtc > DcsSyncDelay) CrosshairButton.IsChecked = state?.CrosshairVisible == true;
    TransferButton.IsEnabled = _items.Count > 0
      && state is { IsEnteringWaypoints: false }
      && DateTimeOffset.UtcNow - _transferClickedUtc > DcsSyncDelay;
    ClearButton.IsEnabled = _items.Count > 0;
    UpdateTransferProgress(state);
    RefreshTransferMarks(state?.AircraftId);
  }

  private void ShowDcsState(DcsState? state)
  {
    DcsStatusText.Text = App.DcsListener.IsListening
      ? "No connection to DCS"
      : $"Port {DcsListener.DefaultPort} is in use by another program. Close it and restart DCSMyWay.";
    DcsStatusText.Visibility = state is null ? Visibility.Visible : Visibility.Collapsed;
    AircraftChip.Visibility = state is null ? Visibility.Collapsed : Visibility.Visible;
    AircraftText.Text = state?.Position.Module ?? "";
    SteerpointChip.Visibility = state?.ActiveSteerpoint is null ? Visibility.Collapsed : Visibility.Visible;
    SteerpointText.Text = state?.ActiveSteerpoint is { } number ? $"STPT {number}" : "";
  }

  private void UpdateTransferProgress(DcsState? state)
  {
    if (!IsTransferInProgress) return;
    if (state?.IsEnteringWaypoints == true)
    {
      _transferStarted = true;
      return;
    }

    if (!_transferStarted && DateTimeOffset.UtcNow - _transferClickedUtc < TransferStartTimeout) return;
    if (MessageBar.Message == _transferMessage) MessageBar.IsOpen = false;
    _transferMessage = null;
  }

  private void RefreshTransferMarks(long? aircraftId)
  {
    for (var i = 0; i < _items.Count; i++)
    {
      var item = _items[i];
      var transferred = !IsTransferInProgress && _transferHistory.Contains(aircraftId, item.Waypoint);
      if (item.IsTransferred != transferred) _items[i] = item with { IsTransferred = transferred };
    }
  }

  private void OnMarkReceived(object? sender, DcsPosition position) =>
    DispatcherQueue.TryEnqueue(async () => await AddWaypointAsync(position));

  private async Task AddWaypointAsync(DcsPosition position)
  {
    var number = _nextWaypointNumber++;
    var waypoint = new Waypoint(_nextWaypointId++, $"W{number}", position.Latitude, position.Longitude, position.ElevationMeters);
    var item = new WaypointItem(waypoint, false);
    _items.Add(item);
    WaypointList.ScrollIntoView(item);
    Refresh();
    await App.DcsSender.ShowStatusAsync($"{waypoint.Name} added");
    await App.DcsSender.ShowRouteAsync(Waypoints);
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
    var state = App.DcsListener.Latest;
    if (state is null || !F16.ModuleNames.Contains(state.Position.Module))
    {
      ShowMessage(InfoBarSeverity.Warning, $"Only the F-16 is supported for now ({state?.Position.Module ?? "no aircraft"})");
      return;
    }

    var waypoints = Waypoints;
    _transferClickedUtc = DateTimeOffset.UtcNow;
    TransferButton.IsEnabled = false;
    if (_transferHistory.IsRepeat(state.AircraftId, waypoints) && await RepeatTransferDialog.ShowAsync() != ContentDialogResult.Primary)
    {
      _transferClickedUtc = default;
      Refresh();
      return;
    }

    _transferClickedUtc = DateTimeOffset.UtcNow;
    var presses = F16.CreateButtonPresses(waypoints.Select(F16.Format), App.Settings.Current.StartAtActiveSteerpoint);
    if (await App.DcsSender.SendWaypointsAsync(presses))
    {
      _transferHistory.Record(state.AircraftId, waypoints);
      _transferMessage = $"Entering {waypoints.Count} waypoints, don't touch the cockpit controls";
      _transferStarted = false;
      ShowMessage(InfoBarSeverity.Informational, _transferMessage);
      return;
    }

    _transferClickedUtc = default;
    ShowMessage(InfoBarSeverity.Error, "DCS is not reachable");
  }

  private async void DeleteWaypointButton_Click(object sender, RoutedEventArgs e)
  {
    if (((FrameworkElement)sender).DataContext is not WaypointItem item) return;
    _items.Remove(item);
    Refresh();
    await App.DcsSender.ShowRouteAsync(Waypoints);
  }

  private async void ClearButton_Click(object sender, RoutedEventArgs e)
  {
    _items.Clear();
    _nextWaypointNumber = 1;
    Refresh();
    await App.DcsSender.ShowRouteAsync(Waypoints);
  }

  private void ShowMessage(InfoBarSeverity severity, string message)
  {
    MessageBar.Severity = severity;
    MessageBar.Message = message;
    MessageBar.IsOpen = true;
  }
}
