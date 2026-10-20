using DCSMyWay.Aircraft;
using DCSMyWay.Models;
using DCSMyWay.Tests.Fixtures;

namespace DCSMyWay.Tests.Aircraft;

public class F16Tests
{
  private static readonly ButtonPress Return = new(17, 3032, 150, -1);
  private static readonly ButtonPress SteerpointPage = new(17, 3006, 150);
  private static readonly ButtonPress Enter = new(17, 3016, 10);
  private static readonly ButtonPress Digit1 = new(17, 3003, 10);
  private static readonly ButtonPress Digit2 = new(17, 3004, 10);
  private static readonly ButtonPress Digit3 = new(17, 3005, 10);

  private static List<ButtonPress> TheWayWaypointPresses(TheWayFixture fixture) =>
    [.. fixture.Buttons.Select(b => b.ToButtonPress()).Skip(3)];

  [Fact]
  public void SelectsTheStartSteerpointAndThenEntersWaypointsLikeTheWay()
  {
    var fixture = TheWayFixture.Load("F16.json");

    var sequence = F16.CreateTransfer(fixture.Waypoints!.Select(w => w.ToCockpitWaypoint()), 3);

    Assert.Equal([Return, Return, Return, SteerpointPage, Digit3, Enter, .. TheWayWaypointPresses(fixture)], sequence.Presses);
  }

  [Fact]
  public void CreatesTheSameWaypointPressesAsTheWayFromDcsCoordinates()
  {
    var fixture = TheWayFixture.Load("F16EndToEnd.json");

    var sequence = F16.CreateTransfer(fixture.Raw!.Select(w => F16.Format(w.ToWaypoint())), 12);

    Assert.Equal([Return, Return, Return, SteerpointPage, Digit1, Digit2, Enter, .. TheWayWaypointPresses(fixture)], sequence.Presses);
  }

  [Fact]
  public void FormatsCoordinatesLikeTheWay()
  {
    var fixture = TheWayFixture.Load("F16EndToEnd.json");

    var formatted = fixture.Raw!.Select(w => F16.Format(w.ToWaypoint()));

    Assert.Equal(fixture.Formatted!.Select(w => w.ToCockpitWaypoint()), formatted);
  }

  [Fact]
  public void FirstWaypointGoesToTheSelectedSteerpointAndTheRestFollowIt()
  {
    var waypoints = TheWayFixture.Load("F16.json").Waypoints!.Select(w => w.ToCockpitWaypoint()).ToList();

    var presses = F16.CreateTransfer(waypoints, 1).Presses;

    Assert.Equal(waypoints.Count - 1, presses.Count(p => p.Code == 3030));
    Assert.NotEqual(3030, presses[6].Code);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(24)]
  public void MarksEachWaypointAsEnteredRightAfterItsElevationEnter(int startSteerpoint)
  {
    var waypoints = TheWayFixture.Load("F16.json").Waypoints!.Select(w => w.ToCockpitWaypoint()).ToList();

    var sequence = F16.CreateTransfer(waypoints, startSteerpoint);

    var waypointEnters = sequence.Presses
      .Select((press, index) => (press, index))
      .Where(p => p.press == Enter)
      .Skip(1)
      .Select(p => p.index + 1)
      .ToList();
    Assert.Equal(waypoints.Count * 3, waypointEnters.Count);
    Assert.Equal(waypointEnters.Where((_, index) => index % 3 == 2), sequence.WaypointEnteredAfter);
    Assert.True(sequence.WaypointEnteredAfter[^1] < sequence.Presses.Count);
  }

  [Fact]
  public void AddsTheExtraDelayToEveryButtonPress()
  {
    var waypoints = TheWayFixture.Load("F16.json").Waypoints!.Select(w => w.ToCockpitWaypoint()).ToList();

    var baseDelays = F16.CreateTransfer(waypoints, 1).Presses.Select(p => p.Delay);
    var delayed = F16.CreateTransfer(waypoints, 1, extraDelay: 50).Presses.Select(p => p.Delay);

    Assert.Equal(baseDelays.Select(d => d + 50), delayed);
  }
}
