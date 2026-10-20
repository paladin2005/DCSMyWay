using DCSMyWay.Models;

namespace DCSMyWay.Services;

public sealed class TransferHistory
{
  private long? _aircraftId;
  private List<Waypoint>? _waypoints;

  public bool IsRepeat(long? aircraftId, IEnumerable<Waypoint> waypoints) =>
    _waypoints is not null && _aircraftId == aircraftId && _waypoints.SequenceEqual(waypoints);

  public bool Contains(long? aircraftId, Waypoint waypoint) =>
    _waypoints is not null && _aircraftId == aircraftId && _waypoints.Contains(waypoint);

  public void Record(long? aircraftId, IEnumerable<Waypoint> waypoints)
  {
    _aircraftId = aircraftId;
    _waypoints = [.. waypoints];
  }

  public void Clear()
  {
    _aircraftId = null;
    _waypoints = null;
  }
}
