using System.Globalization;
using DCSMyWay.Aircraft;
using DCSMyWay.Models;
using DCSMyWay.Tests.Fixtures;

namespace DCSMyWay.Tests.Aircraft;

public class MirageF1Tests
{
  private const int Wheel = 3694;
  private const int Insert = 3711;
  private static readonly TheWayFixture Fixture = TheWayFixture.Load("MirageF1EndToEnd.json");

  private static List<CockpitWaypoint> Waypoints => [.. Fixture.Raw!.Select(w => MirageF1.Format(w.ToWaypoint()))];

  private static List<List<CockpitStep>> WaypointSteps(TransferSequence sequence)
  {
    var segments = new List<List<CockpitStep>>();
    var start = 2;
    foreach (var end in sequence.WaypointEnteredAfter)
    {
      segments.Add(sequence.Steps[start..end]);
      start = end;
    }
    return segments;
  }

  private static List<ButtonPress> Presses(IEnumerable<CockpitStep> steps) => [.. steps.Where(s => s.Press is not null).Select(s => s.Press!)];

  private static string Single(IReadOnlyList<ArgumentCheck>? checks)
  {
    var check = Assert.Single(checks!);
    return string.Join(" ", check.Arguments.Zip(check.Levels, (a, l) => $"{a}={l.ToString(CultureInfo.InvariantCulture)}"));
  }

  [Fact]
  public void FormatsCoordinatesLikeTheWay()
  {
    Assert.Equal(Fixture.Formatted!.Select(w => w.ToCockpitWaypoint()), Waypoints);
  }

  [Fact]
  public void TypesTheSameHemispheresDigitsAndInsertsAsTheWay()
  {
    var theWay = new List<List<ButtonPress>>();
    foreach (var press in Fixture.Buttons.Select(b => b.ToButtonPress()).Skip(2).SkipLast(1))
    {
      if (press.Code == Wheel) theWay.Add([]);
      else theWay[^1].Add(press);
    }

    var ours = WaypointSteps(MirageF1.CreateTransfer(Waypoints, 1)).Select(segment => Presses(segment).Skip(1).ToList());
    Assert.Equal(theWay, ours);
  }

  [Fact]
  public void ShowsWaypointPositionsAndClearsAnEntryLeftPending()
  {
    var steps = MirageF1.CreateTransfer(Waypoints.Take(1), 1).Steps;

    Assert.Equal(new ButtonPress(1, 3690, 300, 0, false), steps[0].Press);
    Assert.Equal("665=0", Single(steps[0].WaitArguments));
    Assert.Equal(new ButtonPress(1, 3707, 100), steps[1].Press);
    Assert.Equal("1274=0", Single(steps[1].UnlessArguments));
    Assert.Equal("1274=0", Single(steps[1].WaitArguments));
  }

  [Fact]
  public void TurnsTheWheelUntilItShowsEachWaypoint()
  {
    var segments = WaypointSteps(MirageF1.CreateTransfer(Waypoints.Take(2), 3));

    Assert.Equal(new ButtonPress(1, Wheel, 100, 0.111, false), segments[0][0].Press);
    Assert.Equal("667=0.222", Single(segments[0][0].UntilArguments));
    Assert.Equal(9, segments[0][0].Tries);
    Assert.Equal("667=0.333", Single(segments[1][0].UntilArguments));
  }

  [Fact]
  public void ChecksTheHemisphereTheTypedDigitsAndTheInsertLight()
  {
    var segment = WaypointSteps(MirageF1.CreateTransfer(Waypoints.Take(1), 1))[0];

    Assert.Equal(3699, segment[1].Press!.Code);
    Assert.Equal("1274=1 1335=1", Single(segment[1].WaitArguments));
    Assert.Equal("1342=0.4 1343=0.4 1344=0 1345=0 1346=0", Single(segment[6].WaitArguments));
    Assert.Equal(Insert, segment[7].Press!.Code);
    Assert.Equal("1274=0 1335=1 1342=0.4 1343=0.4 1344=0 1345=0 1346=0", Single(segment[7].WaitArguments));
    Assert.Equal(3703, segment[8].Press!.Code);
    Assert.Equal("1274=1 1359=1", Single(segment[8].WaitArguments));
    Assert.Equal("1347=0 1348=0.4 1349=0 1350=0 1351=0.8 1352=0.6", Single(segment[14].WaitArguments));
    Assert.Equal(
      "1274=0 1335=1 1359=1 1342=0.4 1343=0.4 1344=0 1345=0 1346=0 1347=0 1348=0.4 1349=0 1350=0 1351=0.8 1352=0.6",
      Single(segment[15].WaitArguments));
    Assert.Equal(16, segment.Count);
  }

  [Fact]
  public void UsesTheSouthAndWestKeysAndLights()
  {
    var segments = WaypointSteps(MirageF1.CreateTransfer(Waypoints, 1));

    Assert.Equal(3705, segments[2][1].Press!.Code);
    Assert.Equal("1274=1 1336=1", Single(segments[2][1].WaitArguments));
    Assert.Equal(3701, segments[4][8].Press!.Code);
    Assert.Equal("1274=1 1360=1", Single(segments[4][8].WaitArguments));
  }

  [Fact]
  public void GivesTheCheckmarkOnceTheLongitudeIsInserted()
  {
    var sequence = MirageF1.CreateTransfer(Waypoints.Take(2), 1);

    var secondInsert = sequence.Steps.Select((s, i) => (s, i)).Where(x => x.s.Press?.Code == Insert).ElementAt(1).i;
    Assert.Equal(secondInsert + 1, sequence.WaypointEnteredAfter[0]);
    Assert.Equal([1, 2], sequence.Steerpoints);
  }

  [Fact]
  public void LeavesThePcnShowingThePresentPosition()
  {
    var steps = MirageF1.CreateTransfer(Waypoints.Take(1), 1).Steps;

    Assert.Equal(new ButtonPress(1, 3690, 300, 0.2, false), steps[^1].Press);
  }

  [Fact]
  public void SupportsOnlyTheMirageWithAnInertialNavigationSystem()
  {
    Assert.Same(MirageF1.Aircraft, SupportedAircraft.Find("Mirage-F1EE"));
    Assert.Null(SupportedAircraft.Find("Mirage-F1CE"));
    Assert.Null(SupportedAircraft.Find("Mirage-F1BE"));
    Assert.Equal((1, 9, false), (MirageF1.Aircraft.FirstSteerpoint, MirageF1.Aircraft.LastSteerpoint, MirageF1.Aircraft.SupportsTargets));
  }
}
