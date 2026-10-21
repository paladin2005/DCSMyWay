using DCSMyWay.Aircraft;
using DCSMyWay.Models;
using DCSMyWay.Tests.Fixtures;

namespace DCSMyWay.Tests.Aircraft;

public class A10Tests
{
  private const int CopyToNewWaypoint = 3007;
  private const int LongitudeKey = 3004;
  private const int ElevationKey = 3002;
  private static readonly TheWayFixture Fixture = TheWayFixture.Load("A10EndToEnd.json");

  private static List<CockpitWaypoint> Waypoints => [.. Fixture.Raw!.Select(w => A10.Format(w.ToWaypoint()))];

  private static List<List<CockpitStep>> WaypointSteps(TransferSequence sequence)
  {
    var segments = new List<List<CockpitStep>>();
    var start = 4;
    foreach (var end in sequence.WaypointEnteredAfter)
    {
      segments.Add(sequence.Steps[start..end]);
      start = end;
    }
    return segments;
  }

  private static List<IndicationCheck> Readbacks(TransferSequence sequence) =>
    [.. sequence.Steps
      .SelectMany(s => s.Wait ?? [])
      .Where(c => c.Element is "^WAYPTLat$" or "^WAYPTLong$" or "^WAYPT_EL3$" or "^WAYPTIdent1$")];

  [Fact]
  public void FormatsCoordinatesLikeTheWay()
  {
    Assert.Equal(Fixture.Formatted!.Select(w => w.ToCockpitWaypoint()), Waypoints);
  }

  [Fact]
  public void TypesTheSameCoordinatesAndNamesAsTheWay()
  {
    var theWay = new List<List<ButtonPress>>();
    foreach (var press in Fixture.Buttons.Select(b => b.ToButtonPress()).Skip(2))
    {
      if (press.Code == CopyToNewWaypoint) theWay.Add([]);
      else theWay[^1].Add(press);
    }

    var ours = WaypointSteps(A10.CreateTransfer(Waypoints, 1)).Select(segment =>
    {
      var presses = segment.Where(s => s.Press is not null).Select(s => s.Press!).ToList();
      var afterCopy = presses.FindIndex(p => p.Code == CopyToNewWaypoint) + 1;
      var longitude = presses.FindIndex(p => p.Code == LongitudeKey);
      var elevation = presses.FindIndex(p => p.Code == ElevationKey);
      return presses[afterCopy..(longitude + 1)].Concat(presses[(elevation + 1)..]).ToList();
    });
    Assert.Equal(theWay, ours);
  }

  [Fact]
  public void PutsTheCduOnTheWaypointPageFirst()
  {
    var opening = A10.CreateTransfer(Waypoints, 1).Steps.Take(4).ToList();

    Assert.Equal(new ButtonPress(22, 3004, 100, 0, false), opening[0].Press);
    Assert.Equal(new ButtonPress(9, 3058, 100), opening[1].Press);
    Assert.Equal((3011, "^PageNameWPMENU$"), (opening[2].Press!.Code, Assert.Single(opening[2].Wait!).Element));
    Assert.Equal((3005, "^PageNameWAYPT$"), (opening[3].Press!.Code, Assert.Single(opening[3].Wait!).Element));
  }

  [Theory]
  [InlineData(1)]
  [InlineData(12)]
  public void SelectsEachWaypointByNumberOrCreatesIt(int start)
  {
    var segments = WaypointSteps(A10.CreateTransfer(Waypoints, start));

    for (var i = 0; i < segments.Count; i++)
    {
      var number = (start + i).ToString();
      var select = segments[i].FindIndex(s => s.Press?.Code == 3001);
      Assert.Equal(number.Select(c => c == '0' ? 3024 : 3015 + c - '1'), segments[i][..select].Select(s => s.Press!.Code));
      Assert.Equal([number, "CDU INPUT ERR"], segments[i][select].Wait!.Select(c => Assert.Single(c.Values!)));
      Assert.Equal(3058, segments[i][select + 1].Press!.Code);
      Assert.Equal(CopyToNewWaypoint, segments[i][select + 2].Press!.Code);
      Assert.Equal(number, Assert.Single(Assert.Single(segments[i][select + 2].Until!).Values!));
    }
  }

  [Fact]
  public void EntersTheElevationAfterTheCoordinatesAndTheNameLast()
  {
    foreach (var segment in WaypointSteps(A10.CreateTransfer(Waypoints, 1)))
    {
      var keys = segment.Where(s => s.Wait is not null && s.Press?.Code is 3002 or 3003 or 3004 or 3005).Select(s => s.Press!.Code);
      Assert.Equal([3003, 3004, 3002, 3005], keys);
    }
  }

  [Fact]
  public void ReadsBackEachValueFromTheCdu()
  {
    var readbacks = Readbacks(A10.CreateTransfer(Waypoints, 1));

    Assert.Equal(Waypoints.Count * 4, readbacks.Count);
    Assert.Equal(["^WAYPTLat$", "^WAYPTLong$", "^WAYPT_EL3$", "^WAYPTIdent1$"], readbacks.Take(4).Select(c => c.Element));
    Assert.Equal(["N43°59.985", "N43°59.984", "N43°59.986"], readbacks[0].Values!);
    Assert.Equal("E040°08.605", readbacks[1].Values![0]);
    Assert.Equal("2412", readbacks[2].Values![0]);
    Assert.Equal(["W1"], readbacks[3].Values!);
    Assert.Equal(["N44°00.000", "N43°59.999", "N44°00.001"], readbacks[4].Values!);
    Assert.Equal("S59°05.599", readbacks[8].Values![0]);
    Assert.Equal("E117°03.146", readbacks[13].Values![0]);
    Assert.Equal("W115°02.052", readbacks[17].Values![0]);
  }

  [Fact]
  public void MarksEachWaypointAfterCheckingItsNameDidNotJumpToAnotherWaypoint()
  {
    var sequence = A10.CreateTransfer(Waypoints, 7);

    for (var i = 0; i < sequence.WaypointEnteredAfter.Count; i++)
    {
      var last = sequence.Steps[sequence.WaypointEnteredAfter[i] - 1];
      Assert.Null(last.Press);
      Assert.Equal((7 + i).ToString(), Assert.Single(Assert.Single(last.Wait!).Values!));
      Assert.Equal($"Another waypoint is already named W{i + 1}", last.Failure);
    }
  }

  [Fact]
  public void SupportsBothA10CModulesWithMissionWaypointsOneToFifty()
  {
    Assert.Same(A10.Aircraft, SupportedAircraft.Find("A-10C"));
    Assert.Same(A10.Aircraft, SupportedAircraft.Find("A-10C_2"));
    Assert.Null(SupportedAircraft.Find("A-10A"));
    Assert.Equal((1, 50), (A10.Aircraft.FirstSteerpoint, A10.Aircraft.LastSteerpoint));
    Assert.Equal("F-16, F/A-18C, A-10C, AH-64D and Ka-50", SupportedAircraft.Names);
  }

  [Theory]
  [InlineData("W1", true)]
  [InlineData("ATTACK", true)]
  [InlineData("TGT.2", true)]
  [InlineData("ABCDEFGHIJKL", true)]
  [InlineData("ABCDEFGHIJKLM", false)]
  [InlineData("1ABC", false)]
  [InlineData("AB CD", false)]
  [InlineData("w1", false)]
  [InlineData("", false)]
  public void AcceptsWaypointNamesTheCduAccepts(string name, bool valid)
  {
    Assert.Equal(valid, Waypoint.IsValidName(name));
  }

  [Fact]
  public void OpensTheActiveFlightPlanAfterTheLastWaypoint()
  {
    var sequence = A10.CreateTransfer(Waypoints.Take(2), 4);

    var flightPlan = sequence.Steps[sequence.WaypointEnteredAfter[^1]..];
    Assert.Equal((3013, "^PageNameFPMENU$"), (flightPlan[0].Press!.Code, Assert.Single(flightPlan[0].Wait!).Element));
    Assert.Equal(["^ActiveFP1$", "^ActiveFP3$", "^ActiveFP5$"], flightPlan[1].Options!.Select(o => o.When.Element));
    Assert.Equal([3005, 3006, 3007], flightPlan[1].Options!.Select(o => o.Press!.Code));
    Assert.Equal(new ButtonPress(9, 3063, 100, -1), flightPlan[1].Otherwise);
    Assert.Equal("^PageNameFPBUILD$", Assert.Single(flightPlan[2].Wait!).Element);
  }

  [Fact]
  public void AddsEachWaypointToTheFlightPlanUnlessItIsAlreadyThere()
  {
    var sequence = A10.CreateTransfer(Waypoints.Take(2), 12);

    var flightPlan = sequence.Steps[(sequence.WaypointEnteredAfter[^1] + 3)..];
    var searches = flightPlan.Where(s => s.Options?.Count == 4).ToList();
    Assert.Equal(2, searches.Count);
    for (var i = 0; i < searches.Count; i++)
    {
      var number = (12 + i).ToString();
      Assert.Equal([number], searches[i].Options![0].When.Values!);
      Assert.Equal(3058, searches[i].Options![0].Press!.Code);
      Assert.Equal(["^WPNewName$", "^WPNewName2$", "^WPNewName4$"], searches[i].Options!.Skip(1).Select(o => o.When.Element));
      Assert.Equal([3002, 3003, 3004], searches[i].Options!.Skip(1).Select(o => o.Press!.Code));
      Assert.Equal(new ButtonPress(9, 3063, 100, -1), searches[i].Otherwise);
      var index = flightPlan.IndexOf(searches[i]);
      Assert.Equal("^1/", Assert.Single(flightPlan[index - 3].Options!).When.Pattern);
      Assert.Equal(new ButtonPress(9, 3062, 100), flightPlan[index - 3].Otherwise);
      Assert.Equal([3015, 3015 + i + 1], flightPlan[(index - 2)..index].Select(s => s.Press!.Code));
      Assert.Equal($"W{i + 1}", Assert.Single(Assert.Single(flightPlan[index + 1].Wait!).Values!));
    }
    Assert.Equal(flightPlan.Count, flightPlan.IndexOf(searches[^1]) + 2);
  }

  [Fact]
  public void NumbersEveryWaypointFromTheStart()
  {
    var sequence = A10.CreateTransfer(Waypoints, 12);

    Assert.Equal(Enumerable.Range(sequence.Steerpoints[0]!.Value, sequence.Steerpoints.Count).Select(n => (int?)n), sequence.Steerpoints);
    Assert.Equal(sequence.WaypointEnteredAfter.Count, sequence.Steerpoints.Count);
  }
}
