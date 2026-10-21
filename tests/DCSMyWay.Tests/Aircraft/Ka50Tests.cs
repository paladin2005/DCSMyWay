using DCSMyWay.Aircraft;
using DCSMyWay.Models;
using DCSMyWay.Tests.Fixtures;

namespace DCSMyWay.Tests.Aircraft;

public class Ka50Tests
{
  private const int WaypointButton = 3011;
  private const int TargetButton = 3017;
  private const int Enter = 3018;
  private static readonly TheWayFixture Fixture = TheWayFixture.Load("KA50EndToEnd.json");

  private static List<Waypoint> Waypoints => [.. Fixture.Raw!.Select(w => w.ToWaypoint())];

  private static List<List<CockpitStep>> PointSteps(TransferSequence sequence)
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

  [Fact]
  public void FormatsCoordinatesLikeTheWay()
  {
    Assert.Equal(Fixture.Formatted!.Select(w => w.ToCockpitWaypoint()), Waypoints.Select(Ka50.Format));
  }

  [Fact]
  public void TypesTheSameNumbersAndCoordinatesAsTheWay()
  {
    var theWay = new List<List<ButtonPress>> { new() };
    foreach (var press in Fixture.Buttons.Select(b => b.ToButtonPress()).Skip(2).SkipLast(1))
    {
      theWay[^1].Add(press);
      if (press.Code == Enter) theWay.Add([]);
    }
    theWay.RemoveAt(theWay.Count - 1);

    var ours = PointSteps(Ka50.CreateTransfer(Waypoints, 1, 1)).Select(segment =>
    {
      var presses = Presses(segment);
      var number = presses.FindIndex(p => p.Code != WaypointButton);
      return presses[number..(presses.FindIndex(p => p.Code == Enter) + 1)];
    });
    Assert.Equal(theWay, ours);
  }

  [Fact]
  public void StartsInEntryModeAndGoesBackToOperation()
  {
    var steps = Ka50.CreateTransfer(Waypoints.Take(1), 1, 1).Steps;

    Assert.Equal(new ButtonPress(20, 3026, 300, 0.3, false), steps[0].Press);
    Assert.Equal(new ButtonPress(20, 3026, 300, 0.2, false), steps[1].Press);
    Assert.Equal(new ButtonPress(20, 3019, 100), steps[^2].Press);
    Assert.Equal(new ButtonPress(20, 3026, 300, 0.3, false), steps[^1].Press);
  }

  [Fact]
  public void ChecksTheWaypointListBeforeSelectingAWaypoint()
  {
    var segment = PointSteps(Ka50.CreateTransfer(Waypoints.Take(1), 4, 1))[0];

    Assert.Equal(WaypointButton, segment[0].Press!.Code);
    Assert.Equal("^txt_OIT_PPM$", Assert.Single(segment[0].Wait!).Element);
    Assert.Equal(new ButtonPress(20, 3005, 100), segment[1].Press);
    Assert.Equal(("^txt_OIT_PPM$", "^%s*4$"), (Assert.Single(segment[1].Wait!).Element, Assert.Single(segment[1].Wait!).Pattern));
  }

  [Fact]
  public void ChecksTheTypedCoordinatesBeforeEnter()
  {
    var segment = PointSteps(Ka50.CreateTransfer(Waypoints.Skip(3).Take(1), 1, 1))[0];

    var typed = segment.Where(s => s.Wait?.Any(c => c.Pattern?.StartsWith("^%s*", StringComparison.Ordinal) == true) == true).ToList();
    Assert.Equal(["^%s*1$", "^%s*55468$", "^%s*117031$"], typed.Select(s => Assert.Single(s.Wait!).Pattern));
    Assert.True(segment.IndexOf(typed[^1]) < segment.FindIndex(s => s.Press?.Code == Enter));
  }

  [Fact]
  public void ReadsEachPointBackFromAFreshSelectionAfterEnter()
  {
    var segment = PointSteps(Ka50.CreateTransfer(Waypoints.Take(1), 2, 1))[0];

    var enter = segment.FindIndex(s => s.Press?.Code == Enter);
    var after = segment[(enter + 1)..];
    Assert.Equal([WaypointButton, WaypointButton, 3003], Presses(after).Select(p => p.Code));
    Assert.Equal("^txt_OIT_PPM$", Assert.Single(after[1].Wait!).Element);
    var latitude = Assert.Single(after[2].Wait!);
    Assert.Equal("^txt_VIT$", latitude.Element);
    Assert.Equal(["44000", " 44000", "43599", " 43599", "44001", " 44001"], latitude.Values);
    var longitude = Assert.Single(after[3].Wait!);
    Assert.Equal("^txt_NIT$", longitude.Element);
    Assert.Contains(" 040086", longitude.Values!);
    Assert.Equal(4, after.Count);
  }

  [Fact]
  public void ChecksTheMinusSignOfSouthernAndWesternCoordinates()
  {
    var segments = PointSteps(Ka50.CreateTransfer(Waypoints, 1, 1));

    List<string> Signs(List<CockpitStep> segment) =>
      [.. segment.SelectMany(s => s.Wait ?? []).Where(c => c.Values is ["-"]).Select(c => c.Element)];
    Assert.Empty(Signs(segments[0]));
    Assert.Equal(["^txt_VIT_sign$", "^txt_VIT_sign$"], Signs(segments[2]));
    Assert.Equal(["^txt_NIT_sign$", "^txt_NIT_sign$"], Signs(segments[4]));
    Assert.Equal(new ButtonPress(20, 3002, 100), Presses(segments[2])[3]);
  }

  [Fact]
  public void TogglesTheWaypointButtonBetweenConsecutiveWaypoints()
  {
    var segments = PointSteps(Ka50.CreateTransfer(Waypoints.Take(2), 1, 1));

    Assert.Equal([WaypointButton, WaypointButton, 3003], Presses(segments[1]).Take(3).Select(p => p.Code));
    Assert.Null(segments[1][0].Wait);
    Assert.NotNull(segments[1][1].Wait);
  }

  [Fact]
  public void EntersTargetsAsNavTargetsWithTheirOwnNumbers()
  {
    List<Waypoint> points = [Waypoints[0], Waypoints[1] with { IsTarget = true }, Waypoints[3]];

    var sequence = Ka50.CreateTransfer(points, 5, 3);

    Assert.Equal([5, null, 6], sequence.Steerpoints);
    var segments = PointSteps(sequence);
    Assert.Equal([TargetButton, 3004], Presses(segments[1]).Take(2).Select(p => p.Code));
    Assert.Equal(("^txt_OIT_NOT$", "^%s*3$"), (Assert.Single(segments[1][1].Wait!).Element, Assert.Single(segments[1][1].Wait!).Pattern));
    Assert.Equal([TargetButton, TargetButton, 3004], Presses(segments[1][(segments[1].FindIndex(s => s.Press?.Code == Enter) + 1)..]).Select(p => p.Code));
    Assert.Equal([WaypointButton, 3007], Presses(segments[2]).Take(2).Select(p => p.Code));
  }

  [Fact]
  public void GivesTheCheckmarkAfterTheStoredCoordinatesAreReadBack()
  {
    var sequence = Ka50.CreateTransfer(Waypoints.Take(2), 1, 1);

    var firstReadback = sequence.Steps.FindIndex(s => s.Wait?.Any(c => c.Element == "^txt_NIT$" && c.Values is not null) == true);
    Assert.Equal(firstReadback + 1, sequence.WaypointEnteredAfter[0]);
  }

  [Fact]
  public void FindsBothVersionsOfTheKa50()
  {
    Assert.Same(Ka50.Aircraft, SupportedAircraft.Find("Ka-50"));
    Assert.Same(Ka50.Aircraft, SupportedAircraft.Find("Ka-50_3"));
    Assert.True(Ka50.Aircraft.SupportsTargets);
    Assert.False(Ka50.Aircraft.SupportsThreats);
    Assert.Equal((1, 6, (int?)9), (Ka50.Aircraft.FirstSteerpoint, Ka50.Aircraft.LastSteerpoint, Ka50.Aircraft.LastTarget));
  }
}
