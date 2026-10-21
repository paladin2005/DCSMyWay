using DCSMyWay.Models;

namespace DCSMyWay.Services;

public sealed record PendingTransfer(List<Waypoint> Waypoints, int? StartSteerpoint);

public sealed class TransferHistory
{
  private readonly Dictionary<Waypoint, int?> _entered = [];
  private long? _aircraftId;
  private int? _transferId;
  private List<Waypoint> _route = [];
  private List<int?> _steerpoints = [];
  private List<int> _waypointEnteredAfter = [];

  public bool IsEntered(long? aircraftId, Waypoint waypoint) => _aircraftId == aircraftId && _entered.ContainsKey(waypoint);

  public int? SteerpointOf(long? aircraftId, Waypoint waypoint) =>
    _aircraftId == aircraftId && _entered.TryGetValue(waypoint, out var steerpoint) ? steerpoint : null;

  public int? FirstSteerpoint(long? aircraftId, IEnumerable<Waypoint> waypoints) =>
    waypoints.Select(w => SteerpointOf(aircraftId, w)).FirstOrDefault(s => s is not null);

  public PendingTransfer Pending(long? aircraftId, IReadOnlyList<Waypoint> waypoints)
  {
    var first = 0;
    while (first < waypoints.Count && IsEntered(aircraftId, waypoints[first])) first++;
    if (first == 0) return new PendingTransfer([.. waypoints], null);
    var last = waypoints.Take(first).Select(w => SteerpointOf(aircraftId, w)).LastOrDefault(s => s is not null);
    return new PendingTransfer([.. waypoints.Skip(first)], last + 1);
  }

  public void Record(
    long? aircraftId,
    int transferId,
    IEnumerable<Waypoint> route,
    IEnumerable<int?> steerpoints,
    IEnumerable<int> waypointEnteredAfter)
  {
    if (_aircraftId != aircraftId) _entered.Clear();
    _aircraftId = aircraftId;
    _transferId = transferId;
    _route = [.. route];
    _steerpoints = [.. steerpoints];
    _waypointEnteredAfter = [.. waypointEnteredAfter];
    foreach (var waypoint in _route) _entered.Remove(waypoint);
  }

  public void UpdateProgress(int? transferId, int pressesDone)
  {
    if (transferId is null || transferId != _transferId) return;
    for (var i = 0; i < _route.Count && i < _waypointEnteredAfter.Count && i < _steerpoints.Count; i++)
    {
      if (_waypointEnteredAfter[i] <= pressesDone) _entered[_route[i]] = _steerpoints[i];
    }
  }

  public void Clear()
  {
    _entered.Clear();
    _aircraftId = null;
    _transferId = null;
    _route = [];
    _steerpoints = [];
    _waypointEnteredAfter = [];
  }
}
