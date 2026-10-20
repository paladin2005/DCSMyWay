using DCSMyWay.Models;

namespace DCSMyWay.Services;

public sealed record PendingTransfer(List<Waypoint> Waypoints, int? StartSteerpoint);

public sealed class TransferHistory
{
  private readonly Dictionary<Waypoint, int> _steerpoints = [];
  private long? _aircraftId;
  private int? _transferId;
  private List<Waypoint> _route = [];
  private int _startSteerpoint;
  private List<int> _waypointEnteredAfter = [];

  public bool IsEntered(long? aircraftId, Waypoint waypoint) => SteerpointOf(aircraftId, waypoint) is not null;

  public int? SteerpointOf(long? aircraftId, Waypoint waypoint) =>
    _aircraftId == aircraftId && _steerpoints.TryGetValue(waypoint, out var steerpoint) ? steerpoint : null;

  public PendingTransfer Pending(long? aircraftId, IReadOnlyList<Waypoint> waypoints)
  {
    var first = 0;
    while (first < waypoints.Count && IsEntered(aircraftId, waypoints[first])) first++;
    if (first == 0) return new PendingTransfer([.. waypoints], null);
    return new PendingTransfer([.. waypoints.Skip(first)], SteerpointOf(aircraftId, waypoints[first - 1]) + 1);
  }

  public void Record(
    long? aircraftId,
    int transferId,
    IEnumerable<Waypoint> route,
    int startSteerpoint,
    IEnumerable<int> waypointEnteredAfter)
  {
    if (_aircraftId != aircraftId) _steerpoints.Clear();
    _aircraftId = aircraftId;
    _transferId = transferId;
    _route = [.. route];
    _startSteerpoint = startSteerpoint;
    _waypointEnteredAfter = [.. waypointEnteredAfter];
    foreach (var waypoint in _route) _steerpoints.Remove(waypoint);
  }

  public void UpdateProgress(int? transferId, int pressesDone)
  {
    if (transferId is null || transferId != _transferId) return;
    for (var i = 0; i < _route.Count && i < _waypointEnteredAfter.Count; i++)
    {
      if (_waypointEnteredAfter[i] <= pressesDone) _steerpoints[_route[i]] = _startSteerpoint + i;
    }
  }

  public void Clear()
  {
    _steerpoints.Clear();
    _aircraftId = null;
    _transferId = null;
    _route = [];
    _waypointEnteredAfter = [];
  }
}
