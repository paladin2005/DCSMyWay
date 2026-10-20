using DCSMyWay.Models;
using DCSMyWay.Services;

namespace DCSMyWay.Tests.Services;

public class MapStateTests
{
  private static readonly Waypoint W1 = new(1, "W1", 41.9, 43.7, 1677.5);
  private static readonly Waypoint W2 = new(2, "W2", 42.1, 43.9, 250);

  [Fact]
  public void SendsTheRouteWithSteerpointsAndTheDcsCamera()
  {
    var state = MapState.Create([W1, W2], waypoint => waypoint == W1 ? 3 : null, new DcsPosition("F-16C_50", 41.5, 43.2, 900));

    Assert.Equal(
      """{"waypoints":[{"name":"W1","lat":41.9,"lon":43.7,"steerpoint":3},{"name":"W2","lat":42.1,"lon":43.9,"steerpoint":null}],"camera":{"lat":41.5,"lon":43.2}}""",
      state.ToJson());
  }

  [Fact]
  public void SendsNoCameraWithoutDcs()
  {
    var state = MapState.Create([], _ => null, null);

    Assert.Equal("""{"waypoints":[],"camera":null}""", state.ToJson());
  }
}
