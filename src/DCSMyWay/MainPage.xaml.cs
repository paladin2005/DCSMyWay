using System.Collections.ObjectModel;
using DCSMyWay.Aircraft;
using DCSMyWay.Models;
using DCSMyWay.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace DCSMyWay;

public sealed partial class MainPage : Page
{
  private static readonly TimeSpan DcsSyncDelay = TimeSpan.FromSeconds(1);
  private static readonly TimeSpan TransferStartTimeout = TimeSpan.FromSeconds(3);
  private const string NameRules = "Up to 12 letters, digits or periods, starting with a letter";

  private readonly ObservableCollection<WaypointItem> _items = [];
  private readonly TransferHistory _transferHistory = new();
  private DispatcherQueueTimer? _refreshTimer;
  private DateTimeOffset _crosshairClickedUtc;
  private DateTimeOffset _transferClickedUtc;
  private string? _transferMessage;
  private bool _transferStarted;
  private int _nextWaypointId = 1;
  private int _nextTransferId = 1;
  private int? _sentTransferId;
  private int? _failureShownTransferId;
  private int _nextWaypointNumber = 1;
  private bool _wasConnected;
  private WaypointItem? _editingItem;

  public MainPage()
  {
    InitializeComponent();
    WaypointList.ItemsSource = _items;
    NameBox.MaxLength = Waypoint.MaxNameLength;
    ThreatBox.ItemsSource = ApacheThreats.All;
    TransferButton.IsEnabledChanged += (_, _) =>
      VisualStateManager.GoToState(TransferButton, TransferButton.IsEnabled ? "Normal" : "Disabled", true);
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
    UpdateTransferProgress(state);
    ShowTransferFailure(state);
    var cancelling = IsTransferInProgress || state?.IsEnteringWaypoints == true;
    ShowTransferButton(cancelling);
    var settled = DateTimeOffset.UtcNow - _transferClickedUtc > DcsSyncDelay;
    TransferButton.IsEnabled = state is not null && !cancelling && _items.Count > 0 && settled;
    CancelTransferButton.IsEnabled = state is not null && cancelling && settled;
    ClearButton.IsEnabled = _items.Count > 0;
    if (state is not null) _transferHistory.UpdateProgress(state.TransferId, state.PressesDone);
    RefreshTransferMarks(state?.AircraftId);
    var supportsTargets = SupportedAircraft.Find(state?.Position.Module)?.SupportsTargets == true;
    foreach (var item in _items) item.ShowsTargetToggle = supportsTargets;
  }

  private void ShowTransferButton(bool cancelling)
  {
    ShowOverlappingButton(TransferButton, !cancelling);
    ShowOverlappingButton(CancelTransferButton, cancelling);
  }

  private static void ShowOverlappingButton(Control button, bool visible)
  {
    button.Opacity = visible ? 1 : 0;
    button.IsHitTestVisible = visible;
    button.IsTabStop = visible;
    AutomationProperties.SetAccessibilityView(button, visible ? AccessibilityView.Content : AccessibilityView.Raw);
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
    var label = SupportedAircraft.Find(state?.Position.Module)?.SteerpointLabel ?? "STPT";
    SteerpointText.Text = state?.ActiveSteerpoint is { } number ? $"{label} {number}" : "";
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

  private void ShowTransferFailure(DcsState? state)
  {
    if (state?.TransferFailure is not { } failure || state.TransferId != _sentTransferId || _sentTransferId == _failureShownTransferId) return;
    _failureShownTransferId = _sentTransferId;
    _transferMessage = null;
    ShowMessage(InfoBarSeverity.Error, $"Transfer stopped: {failure}. Waypoints with a checkmark were entered");
  }

  private void RefreshTransferMarks(long? aircraftId)
  {
    foreach (var item in _items) item.IsTransferred = _transferHistory.IsEntered(aircraftId, item.Waypoint);
  }

  private void OnMarkReceived(object? sender, DcsPosition position) =>
    DispatcherQueue.TryEnqueue(async () => await AddWaypointAsync(position));

  private async Task AddWaypointAsync(DcsPosition position)
  {
    while (IsNameTaken($"W{_nextWaypointNumber}", null)) _nextWaypointNumber++;
    var number = _nextWaypointNumber++;
    var waypoint = new Waypoint(_nextWaypointId++, $"W{number}", position.Latitude, position.Longitude, position.ElevationMeters);
    var item = new WaypointItem(waypoint);
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

  private async void TransferButton_Click(SplitButton sender, SplitButtonClickEventArgs args) => await TransferAsync(null);

  private async void OverwriteFromActiveItem_Click(object sender, RoutedEventArgs e) => await TransferAsync(true);

  private async void AddAfterActiveItem_Click(object sender, RoutedEventArgs e) => await TransferAsync(false);

  private async void CancelTransferButton_Click(object sender, RoutedEventArgs e) => await CancelTransferAsync();

  private void TransferMenu_Opening(object sender, object e)
  {
    var state = App.DcsListener.Latest;
    var aircraft = SupportedAircraft.Find(state?.Position.Module);
    var active = state?.ActiveSteerpoint;
    var steerpoint = active is { } number
      ? $"{aircraft?.SteerpointLabel ?? "STPT"} {number}"
      : $"the active {aircraft?.SteerpointName ?? "steerpoint"}";
    OverwriteFromActiveItem.Text = $"Overwrite from {steerpoint}";
    AddAfterActiveItem.Text = $"Add after {steerpoint}";
    OverwriteFromActiveItem.IsEnabled = active is not null;
    AddAfterActiveItem.IsEnabled = active is not null;
  }

  private async Task TransferAsync(bool? startAtActive)
  {
    var state = App.DcsListener.Latest;
    if (IsTransferInProgress || state?.IsEnteringWaypoints == true) return;
    var aircraft = SupportedAircraft.Find(state?.Position.Module);
    if (state is null || aircraft is null)
    {
      ShowMessage(InfoBarSeverity.Warning, $"Only the {SupportedAircraft.Names} are supported for now ({state?.Position.Module ?? "no aircraft"})");
      return;
    }

    if (!App.DcsListener.IsCockpitConnected)
    {
      ShowMessage(InfoBarSeverity.Warning, @"The DCSMyWay export script is not running. Check its line in Saved Games\DCS\Scripts\Export.lua");
      return;
    }

    var waypoints = Waypoints;
    var pending = _transferHistory.Pending(state.AircraftId, waypoints);
    var again = pending.Waypoints.Count == 0;
    int start;
    if (startAtActive is { } atActive)
    {
      if (state.ActiveSteerpoint is not { } active)
      {
        ShowMessage(InfoBarSeverity.Warning, aircraft.SteerpointHint);
        return;
      }

      start = Math.Max(atActive ? active : active + 1, aircraft.FirstSteerpoint);
      if (again) pending = new PendingTransfer(waypoints, start);
      again = false;
    }
    else if (again)
    {
      start = _transferHistory.FirstSteerpoint(state.AircraftId, waypoints) ?? aircraft.FirstSteerpoint;
      pending = new PendingTransfer(waypoints, start);
    }
    else if (pending.StartSteerpoint is { } next)
    {
      start = next;
    }
    else if (state.ActiveSteerpoint is { } active)
    {
      start = Math.Max(active, aircraft.FirstSteerpoint);
    }
    else
    {
      ShowMessage(InfoBarSeverity.Warning, aircraft.SteerpointHint);
      return;
    }

    var firstTarget = waypoints.Take(waypoints.Count - pending.Waypoints.Count).Count(w => w.IsTarget) + 1;
    var sequence = aircraft.CreateTransfer(pending.Waypoints, new TransferStart(start, firstTarget, state.Seat));
    if (sequence.Steerpoints.Max() > aircraft.LastSteerpoint)
    {
      ShowMessage(InfoBarSeverity.Warning, $"These waypoints don't fit: the {aircraft.Name} has {aircraft.SteerpointName}s up to {aircraft.LastSteerpoint}");
      return;
    }

    var targets = sequence.Steerpoints.Count(s => s is null);
    if (targets > 0 && firstTarget + targets - 1 > aircraft.LastTarget)
    {
      ShowMessage(InfoBarSeverity.Warning, $"These targets don't fit: the {aircraft.Name} has targets up to {aircraft.LastTarget}");
      return;
    }

    if (again && !await ConfirmTransferAgainAsync(aircraft, start)) return;
    _transferClickedUtc = DateTimeOffset.UtcNow;
    TransferButton.IsEnabled = false;
    var transferId = _nextTransferId++;
    if (await App.DcsSender.SendWaypointsAsync(transferId, sequence.Steps))
    {
      _sentTransferId = transferId;
      _transferHistory.Record(state.AircraftId, transferId, pending.Waypoints, sequence.Steerpoints, sequence.WaypointEnteredAfter);
      _transferMessage = $"Entering {DescribeTransfer(aircraft, sequence, start)}, don't touch the cockpit controls";
      _transferStarted = false;
      ShowMessage(InfoBarSeverity.Informational, _transferMessage);
      await App.DcsSender.SetCrosshairVisibleAsync(false);
      return;
    }

    _transferClickedUtc = default;
    ShowMessage(InfoBarSeverity.Error, "DCS is not reachable");
  }

  private async Task<bool> ConfirmTransferAgainAsync(SupportedAircraft aircraft, int start)
  {
    _transferClickedUtc = DateTimeOffset.UtcNow;
    TransferButton.IsEnabled = false;
    RepeatTransferText.Text = $"All these waypoints are already in this aircraft. They will be entered again starting at {aircraft.SteerpointName} {start}.";
    if (await RepeatTransferDialog.ShowAsync() == ContentDialogResult.Primary) return true;
    _transferClickedUtc = default;
    Refresh();
    return false;
  }

  private async Task CancelTransferAsync()
  {
    _transferClickedUtc = DateTimeOffset.UtcNow;
    CancelTransferButton.IsEnabled = false;
    if (!await App.DcsSender.CancelWaypointsAsync())
    {
      ShowMessage(InfoBarSeverity.Error, "DCS is not reachable");
      return;
    }

    _transferMessage = null;
    ShowMessage(InfoBarSeverity.Warning, "Transfer cancelled. Waypoints with a checkmark were entered");
  }

  public static string ThreatLabel(Waypoint waypoint) =>
    waypoint.IsTarget && waypoint.Threat is not null ? $"  {ApacheThreats.Find(waypoint.Threat).Name}" : "";

  private async void EditWaypointButton_Click(object sender, RoutedEventArgs e)
  {
    if (((FrameworkElement)sender).DataContext is not WaypointItem item) return;
    _editingItem = item;
    NameBox.Text = item.Waypoint.Name;
    ThreatBox.SelectedItem = ApacheThreats.Find(item.Waypoint.Threat);
    var supportsThreats = SupportedAircraft.Find(App.DcsListener.Latest?.Position.Module)?.SupportsThreats == true;
    ThreatBox.Visibility = supportsThreats && item.Waypoint.IsTarget ? Visibility.Visible : Visibility.Collapsed;
    ValidateName();
    if (await App.MainWindow.ShowWithKeyboardAsync(EditDialog) != ContentDialogResult.Primary) return;
    var threat = ThreatBox.Visibility == Visibility.Visible ? (ThreatBox.SelectedItem as ApacheThreat)?.Code : item.Waypoint.Threat;
    var edited = item.Waypoint with { Name = NameBox.Text, Threat = threat };
    if (edited == item.Waypoint) return;
    item.Waypoint = edited;
    Refresh();
    await App.DcsSender.ShowRouteAsync(Waypoints);
  }

  private void EditDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
  {
    NameBox.Focus(FocusState.Programmatic);
    NameBox.SelectAll();
  }

  private void NameBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateName();

  private void ValidateName()
  {
    var name = NameBox.Text;
    var taken = IsNameTaken(name, _editingItem);
    EditDialog.IsPrimaryButtonEnabled = Waypoint.IsValidName(name) && !taken;
    NameBox.Description = taken ? "Another waypoint already has this name" : NameRules;
  }

  private bool IsNameTaken(string name, WaypointItem? except) =>
    _items.Any(i => i != except && i.Waypoint.Name == name);

  private async void TargetToggle_Click(object sender, RoutedEventArgs e)
  {
    if (((FrameworkElement)sender).DataContext is not WaypointItem item) return;
    item.Waypoint = item.Waypoint with { IsTarget = !item.Waypoint.IsTarget };
    Refresh();
    await App.DcsSender.ShowRouteAsync(Waypoints);
  }

  private static string DescribeTransfer(SupportedAircraft aircraft, TransferSequence sequence, int start)
  {
    var waypoints = sequence.Steerpoints.Count(s => s is not null);
    var targets = sequence.Steerpoints.Count - waypoints;
    var route = $"{waypoints} waypoints from {aircraft.SteerpointName} {start}";
    if (targets == 0) return route;
    return waypoints == 0 ? $"{targets} targets" : $"{route} and {targets} targets";
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
