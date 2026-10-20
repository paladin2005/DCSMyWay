using DCSMyWay.Aircraft;
using DCSMyWay.Models;
using DCSMyWay.Tests.Fixtures;

namespace DCSMyWay.Tests.Aircraft;

public class F18Tests
{
  private static readonly TheWayFixture Fixture = TheWayFixture.Load("FA18EndToEnd.json");

  private static List<CockpitWaypoint> Waypoints => [.. Fixture.Raw!.Select(w => F18.Format(w.ToWaypoint()))];

  private static IEnumerable<CockpitStep> Readbacks(TransferSequence sequence) =>
    sequence.Steps.Where(s => s.Wait?.Any(c => c.Indicator == 4) == true);

  [Fact]
  public void FormatsCoordinatesLikeTheWay()
  {
    Assert.Equal(Fixture.Formatted!.Select(w => w.ToCockpitWaypoint()), Waypoints);
  }

  [Fact]
  public void TypesTheSameUfcPressesAsTheWay()
  {
    var sequence = F18.CreateTransfer(Waypoints, 1);

    var typed = sequence.Steps
      .Take(sequence.WaypointEnteredAfter[^1])
      .Where(s => s.Press is not null && s.Until is null && s.Select is null)
      .Select(s => s.Press!);
    var theWay = Fixture.Buttons
      .Select(b => b.ToButtonPress())
      .Skip(4)
      .Where(p => p is not { Device: 37, Code: 3022 });
    Assert.Equal(theWay, typed);
  }

  [Fact]
  public void OpensHsiWaypointDataWithDecimalMinutesAndPreciseBeforeTyping()
  {
    var opening = F18.CreateTransfer(Waypoints, 1).Steps.Take(7).ToList();

    Assert.Equal([3028, 3012, 3020, 3016, 3025, 3017, 3029], opening.Select(s => s.Press!.Code));
    Assert.All(opening, step => Assert.Equal(37, step.Press!.Device));
    Assert.All(opening, step => Assert.NotNull(step.Until));
    Assert.Equal(3, opening[0].Tries);
    Assert.Equal(["^Menu_SUPT$", "^HSELData_Label$", "^MDATA_"], opening[0].Until!.Select(c => c.Element));
    Assert.Equal("^PRECISE_%d+_box", Assert.Single(opening[6].Until!).Element);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(12)]
  public void SelectsEachWaypointByNumberAndEndsOnTheFirstOne(int start)
  {
    var sequence = F18.CreateTransfer(Waypoints, start);

    var targets = sequence.Steps.Where(s => s.Select is not null).Select(s => s.Select!.Target);
    Assert.Equal([.. Enumerable.Range(start, Waypoints.Count), start], targets);
    Assert.All(sequence.Steps.Where(s => s.Select is not null), step =>
    {
      Assert.Equal(new ButtonPress(37, 3022, 100), step.Press);
      Assert.Equal(new ButtonPress(37, 3023, 100), step.Select!.Decrease);
      Assert.Equal("^WYPT_Number$", step.Select.Element);
    });
  }

  [Fact]
  public void ReturnsTheAmpcdToTheHsiAtTheEnd()
  {
    var last = F18.CreateTransfer(Waypoints, 1).Steps[^1];

    Assert.Equal(new ButtonPress(37, 3020, 100), last.Press);
    Assert.Equal("^HSELData_Label$", Assert.Single(last.Until!).Element);
  }

  [Fact]
  public void ReadsBackEachWaypointFromTheAmpcd()
  {
    var sequence = F18.CreateTransfer(Waypoints, 1);

    var readbacks = Readbacks(sequence).Select(s => Assert.Single(s.Wait!)).ToList();
    Assert.Equal(Waypoints.Count * 3, readbacks.Count);
    Assert.Equal(["^WYPT_Longitude$", "^WYPT_Latitude$", "^WYPT_Elev$"], readbacks.Take(3).Select(c => c.Element));
    Assert.Contains("E 40°08.6051'", readbacks[0].Values!);
    Assert.Contains("N 43°59.9854'", readbacks[1].Values!);
    Assert.Contains("2412FT", readbacks[2].Values!);
    Assert.Contains("S 59°05.5992'", readbacks[7].Values!);
    Assert.Contains("E117°03.1458'", readbacks[9].Values!);
    Assert.Contains("W115°02.0520'", readbacks[12].Values!);
  }

  [Fact]
  public void AcceptsOneTenThousandthOfAMinuteOfRoundingInTheReadback()
  {
    var readbacks = Readbacks(F18.CreateTransfer(Waypoints, 1)).Select(s => s.Wait![0]).ToList();

    Assert.Equal(["N 44°00.0000'", "N 43°59.9999'", "N 44°00.0001'"], readbacks[4].Values!);
    Assert.Equal(["0FT", "-1FT", "1FT"], readbacks[5].Values!);
  }

  [Fact]
  public void MarksEachWaypointAsEnteredWhenItsElevationReadsBack()
  {
    var sequence = F18.CreateTransfer(Waypoints, 1);

    var elevationReadbacks = sequence.Steps
      .Select((step, index) => (step, index))
      .Where(s => s.step.Wait?.Any(c => c.Element == "^WYPT_Elev$") == true)
      .Select(s => s.index + 1);
    Assert.Equal(elevationReadbacks, sequence.WaypointEnteredAfter);
  }

  [Fact]
  public void ChecksTheUfcBeforeTypingEachCoordinate()
  {
    var sequence = F18.CreateTransfer(Waypoints.Take(1), 1);

    var ufcChecks = sequence.Steps.SelectMany(s => s.Wait ?? []).Where(c => c.Indicator == 6).ToList();
    Assert.Equal(["POSN", ":", "N", "E"], ufcChecks.Select(c => Assert.Single(c.Values!)));
  }

  [Fact]
  public void FindsEachSupportedAircraftByItsModuleName()
  {
    Assert.Same(F18.Aircraft, SupportedAircraft.Find("FA-18C_hornet"));
    Assert.Same(F16.Aircraft, SupportedAircraft.Find("F-16C_50"));
    Assert.Null(SupportedAircraft.Find("Su-27"));
    Assert.Null(SupportedAircraft.Find(null));
  }

  [Fact]
  public void SkipsWaypointZeroAndStopsAtFiftyNine()
  {
    Assert.Equal((1, 59), (F18.Aircraft.FirstSteerpoint, F18.Aircraft.LastSteerpoint));
  }
}
