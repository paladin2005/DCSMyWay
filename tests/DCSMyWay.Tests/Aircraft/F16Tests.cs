using DCSMyWay.Aircraft;
using DCSMyWay.Tests.Fixtures;

namespace DCSMyWay.Tests.Aircraft;

public class F16Tests
{
  [Fact]
  public void CreatesTheSameButtonPressesAsTheWay()
  {
    var fixture = TheWayFixture.Load("F16.json");

    var presses = F16.CreateButtonPresses(fixture.Waypoints!.Select(w => w.ToCockpitWaypoint()));

    Assert.Equal(fixture.Buttons.Select(b => b.ToButtonPress()), presses);
  }

  [Fact]
  public void FormatsCoordinatesLikeTheWay()
  {
    var fixture = TheWayFixture.Load("F16EndToEnd.json");

    var formatted = fixture.Raw!.Select(w => F16.Format(w.ToWaypoint()));

    Assert.Equal(fixture.Formatted!.Select(w => w.ToCockpitWaypoint()), formatted);
  }

  [Fact]
  public void CreatesTheSameButtonPressesAsTheWayFromDcsCoordinates()
  {
    var fixture = TheWayFixture.Load("F16EndToEnd.json");

    var presses = F16.CreateButtonPresses(fixture.Raw!.Select(w => F16.Format(w.ToWaypoint())));

    Assert.Equal(fixture.Buttons.Select(b => b.ToButtonPress()), presses);
  }

  [Fact]
  public void AddsTheExtraDelayToEveryButtonPress()
  {
    var waypoints = TheWayFixture.Load("F16.json").Waypoints!.Select(w => w.ToCockpitWaypoint()).ToList();

    var baseDelays = F16.CreateButtonPresses(waypoints).Select(p => p.Delay);
    var delayed = F16.CreateButtonPresses(waypoints, extraDelay: 50).Select(p => p.Delay);

    Assert.Equal(baseDelays.Select(d => d + 50), delayed);
  }
}
