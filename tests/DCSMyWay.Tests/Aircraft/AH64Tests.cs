using DCSMyWay.Aircraft;
using DCSMyWay.Models;
using DCSMyWay.Tests.Fixtures;

namespace DCSMyWay.Tests.Aircraft;

public class AH64Tests
{
  private const int Enter = 3006;
  private const int Clear = 3001;
  private static readonly TheWayFixture Fixture = TheWayFixture.Load("AH64EndToEnd.json");

  private static List<Waypoint> Waypoints => [.. Fixture.Raw!.Select(w => w.ToWaypoint())];

  private static List<List<CockpitStep>> PointSteps(TransferSequence sequence)
  {
    var segments = new List<List<CockpitStep>>();
    var start = 0;
    foreach (var end in sequence.WaypointEnteredAfter)
    {
      segments.Add(sequence.Steps[start..end]);
      start = end;
    }
    return segments;
  }

  private static List<int> KeysAfter(List<ButtonPress> presses, int start) =>
    [.. presses.Skip(start).TakeWhile(p => p.Code != Enter).Select(p => p.Code)];

  [Fact]
  public void TypesTheSameTextAndAltitudeAsTheWay()
  {
    var theWay = new List<List<ButtonPress>>();
    foreach (var press in Fixture.Buttons.Select(b => b.ToButtonPress()).Skip(2))
    {
      if (press is { Device: 43, Code: 3023 }) theWay.Add([]);
      else theWay[^1].Add(press);
    }

    var ours = PointSteps(AH64.CreateTransfer(Waypoints, 1, 0));

    Assert.Equal(theWay.Count, ours.Count);
    for (var i = 0; i < ours.Count; i++)
    {
      var theWayKeys = theWay[i].Where(p => p.Device == 29).ToList();
      var theWayName = KeysAfter(theWayKeys, theWayKeys.FindIndex(p => p.Code == Enter) + 1);
      var theWayAltitude = KeysAfter(theWayKeys, theWayKeys.FindLastIndex(p => p.Code == Clear) + 1);
      var ourKeys = ours[i].Where(s => s.Press?.Device == 29 && s.Unless is null).Select(s => s.Press!).ToList();
      var ourName = KeysAfter(ourKeys, ourKeys.FindIndex(p => p.Code == Clear) + 1);
      var ourAltitude = KeysAfter(ourKeys, ourKeys.FindLastIndex(p => p.Code == Clear) + 1);
      Assert.Equal(theWayName, ourName);
      Assert.Equal(theWayAltitude, ourAltitude);
    }
  }

  [Theory]
  [InlineData(0, 43, 29, 9, 16)]
  [InlineData(null, 43, 29, 9, 16)]
  [InlineData(1, 45, 30, 13, 15)]
  public void UsesTheDisplaysAndKeyboardOfTheSeat(int? seat, int mpd, int keyboard, int mpdIndicator, int keyboardIndicator)
  {
    var steps = AH64.CreateTransfer(Waypoints.Take(2), 3, seat).Steps;

    var devices = steps.SelectMany(s => new[] { s.Press, s.Otherwise }.Concat(s.Options?.Select(o => o.Press) ?? []))
      .OfType<ButtonPress>()
      .Select(p => p.Device)
      .Distinct();
    var indicators = steps.SelectMany(s => (s.Until ?? []).Concat(s.Wait ?? []).Concat(s.Unless ?? []).Concat(s.Options?.Select(o => o.When) ?? []))
      .Select(c => c.Indicator)
      .Distinct();
    Assert.Equal(new[] { mpd, keyboard }.Order(), devices.Order());
    Assert.Equal(new[] { mpdIndicator, keyboardIndicator }.Order(), indicators.Order());
  }

  [Fact]
  public void EntersCoordinatesAsMgrsAndReadsThemBack()
  {
    var waypoint = Waypoints[0];
    var coordinate = Mgrs.FromLatLon(waypoint.Latitude, waypoint.Longitude);

    var segment = PointSteps(AH64.CreateTransfer([waypoint], 1, 0))[0];

    var presses = segment.Where(s => s.Press is not null && s.Unless is null).Select(s => s.Press!).ToList();
    var utmClear = presses.FindIndex(presses.FindIndex(p => p is { Device: 29, Code: Clear }) + 1, p => p is { Device: 29, Code: Clear });
    var typed = KeysAfter(presses, utmClear + 1);
    Assert.Equal(coordinate.Entry.Length, typed.Count);
    Assert.Contains(segment, s => s.Wait?.Any(c => c.Element == "^ActiveWaypointStatusWindowPOINT_text_8$" && c.Values![0] == coordinate.Display) == true);
    Assert.Contains(segment, s => s.Wait?.Any(c => c.Element == "^ActiveWaypointStatusWindowPOINT_text_13$" && c.Values![0] == "2412 FT") == true);
    Assert.Contains(segment, s => s.Wait?.Any(c => c.Element == "^ActiveWaypointStatusWindowPOINT_text_3$" && c.Values![0] == "W1") == true);
  }

  [Fact]
  public void AddsThePointWhenItIsTheNextFreeOneAndEditsItOtherwise()
  {
    var segment = PointSteps(AH64.CreateTransfer(Waypoints.Take(1), 5, 0))[0];

    var decision = Assert.Single(segment, s => s.Options?.Count == 2);
    Assert.Equal(["W05", "IDENT>"], decision.Options!.Select(o => Assert.Single(o.When.Values!)));
    Assert.Equal([3024, 3023], decision.Options!.Select(o => o.Press!.Code));
    var editing = segment.Where(s => s.Unless?.Any(c => c.Pattern == "^IDENT:") == true).ToList();
    Assert.Equal([3024, 3029, 3043, 3037, 3006, 3022, 3024], editing.Select(s => s.Press!.Code));
    Assert.Equal("W05 does not exist and is not the next free point", editing[4].Failure);
    var acceptIdentifier = Assert.Single(segment, s => s.Unless?.Any(c => c.Pattern == "^FREE:") == true);
    Assert.Equal(new ButtonPress(29, Enter, 100), acceptIdentifier.Press);
  }

  [Fact]
  public void ChoosesTheWaypointTypeSoTheAttackPhaseDoesNotCreateTargets()
  {
    var segment = PointSteps(AH64.CreateTransfer(Waypoints.Take(1), 5, 0))[0];

    Assert.Equal(new ButtonPress(43, 3022, 100), segment[3].Press);
    Assert.Equal("WP", Assert.Single(Assert.Single(segment[3].Wait!).Values!));
  }

  [Fact]
  public void AddsEachPointToTheEndOfTheRouteUnlessItIsAlreadyThere()
  {
    var sequence = AH64.CreateTransfer(Waypoints.Take(2), 7, 0);

    var searches = sequence.Steps.Where(s => s.Options?.Count == 8).ToList();
    Assert.Equal(2, searches.Count);
    Assert.Equal(2, sequence.Steps.Count(s => s.Press?.Code == 3014));
    Assert.All(searches, search => Assert.True(sequence.Steps.IndexOf(search) < sequence.WaypointEnteredAfter[searches.IndexOf(search)]));
    Assert.All(searches.Select((search, i) => (search, number: 7 + i)), item =>
    {
      Assert.Equal(Enumerable.Repeat($"^..{item.number:00}$", 4), item.search.Options!.Take(4).Select(o => o.When.Pattern));
      Assert.All(item.search.Options!.Take(4), o => Assert.Equal(3023, o.Press!.Code));
      Assert.Equal([3008, 3009, 3010, 3011], item.search.Options!.Skip(4).Select(o => o.Press!.Code));
      Assert.Equal(new ButtonPress(43, 3007, 100), item.search.Otherwise);
    });
    Assert.Equal(new ButtonPress(43, 3029, 100), sequence.Steps[^1].Press);
  }

  [Theory]
  [InlineData("W1", "W1")]
  [InlineData("ATTACK", "ATT")]
  [InlineData("TGT.2", "TGT")]
  [InlineData("A.B.C.D", "ABC")]
  public void KeepsTheFirstThreeLettersOrDigitsAsFreeText(string name, string freeText)
  {
    Assert.Equal(freeText, AH64.FreeText(name));
  }

  [Fact]
  public void SupportsTheApacheWithWaypointsOneToFifty()
  {
    Assert.Same(AH64.Aircraft, SupportedAircraft.Find("AH-64D_BLK_II"));
    Assert.Equal((1, 50), (AH64.Aircraft.FirstSteerpoint, AH64.Aircraft.LastSteerpoint));
    Assert.Equal("W07", AH64.PointIndex(7));
  }

  [Fact]
  public void EntersTargetsAsNewTgPointsAndKeepsWaypointNumbersForTheRest()
  {
    List<Waypoint> waypoints = [Waypoints[0], Waypoints[1] with { IsTarget = true }, Waypoints[2]];

    var sequence = AH64.CreateTransfer(waypoints, 5, 0);

    Assert.Equal([5, null, 6], sequence.Steerpoints);
    var target = PointSteps(sequence)[1][2..];
    Assert.Equal(new ButtonPress(43, 3023, 100), target[0].Press);
    Assert.Equal((3019, "TG"), (target[1].Press!.Code, Assert.Single(Assert.Single(target[1].Wait!).Values!)));
    Assert.Equal((3024, "^IDENT:"), (target[2].Press!.Code, Assert.Single(target[2].Wait!).Pattern));
    Assert.Equal((3006, "^FREE:"), (target[3].Press!.Code, Assert.Single(target[3].Wait!).Pattern));
    Assert.DoesNotContain(target, s => s.Options is not null || s.Unless is not null);
    Assert.Contains(target, s => s.Wait?.Any(c => c.Element == "^ActiveWaypointStatusWindowPOINT_text_1$" && c.Pattern == "^T%d%d$") == true);
    Assert.Contains(target, s => s.Wait?.Any(c => c.Element == "^ActiveWaypointStatusWindowPOINT_text_3$" && c.Values![0] == "W2") == true);
  }

  [Fact]
  public void LeavesTargetsOutOfTheRoute()
  {
    List<Waypoint> waypoints = [Waypoints[0], Waypoints[1] with { IsTarget = true }];

    var sequence = AH64.CreateTransfer(waypoints, 5, 0);

    var searches = sequence.Steps.Where(s => s.Options?.Count == 8).ToList();
    Assert.Equal("^..05$", Assert.Single(searches).Options![0].When.Pattern);
  }

  [Fact]
  public void SkipsTheRouteWhenEveryPointIsATarget()
  {
    var sequence = AH64.CreateTransfer([Waypoints[0] with { IsTarget = true }], 5, 0);

    Assert.Equal([null], sequence.Steerpoints);
    Assert.DoesNotContain(sequence.Steps, s => s.Press?.Code == 3014);
    Assert.Equal(new ButtonPress(43, 3029, 100), sequence.Steps[^1].Press);
    Assert.True(AH64.Aircraft.SupportsTargets);
    Assert.False(F16.Aircraft.SupportsTargets);
  }

  [Fact]
  public void TypesTheThreatCodeAsTheTargetIdentifierAndReadsItBack()
  {
    var sequence = AH64.CreateTransfer([Waypoints[0] with { IsTarget = true, Threat = "15" }], 5, 0);

    var target = PointSteps(sequence)[0][2..];
    Assert.Equal([3033, 3037, 3006], target[3..6].Select(s => s.Press!.Code));
    Assert.Equal("^FREE:", Assert.Single(target[5].Wait!).Pattern);
    Assert.Equal("15", Assert.Single(Assert.Single(target[^1].Wait!).Values!));
    Assert.Equal("^ActiveWaypointStatusWindowPOINT_text_2$", Assert.Single(target[^1].Wait!).Element);
  }

  [Fact]
  public void AGenericTargetKeepsTheTgIdentifier()
  {
    var target = PointSteps(AH64.CreateTransfer([Waypoints[0] with { IsTarget = true }], 5, 0))[0][2..];

    Assert.Equal((3006, "^FREE:"), (target[3].Press!.Code, Assert.Single(target[3].Wait!).Pattern));
    Assert.Equal("TG", Assert.Single(Assert.Single(target[^1].Wait!).Values!));
  }

  [Fact]
  public void ClearsTheFreeTextWithClrBecauseBksOnAnEmptyEntryCancelsThePrompt()
  {
    var segment = PointSteps(AH64.CreateTransfer(Waypoints.Take(1), 5, 0))[0];

    Assert.DoesNotContain(segment, s => s.Press?.Code == 3002);
    var clear = Assert.Single(segment, s => s.Wait?.Any(c => c.Pattern == "^FREE:#?$") == true);
    Assert.Equal(new ButtonPress(29, 3001, 100), clear.Press);
  }

  [Fact]
  public void GivesTheCheckmarkOnlyOnceThePointIsInTheRoute()
  {
    var sequence = AH64.CreateTransfer(Waypoints.Take(2), 5, 0);

    var firstRouteCheck = sequence.Steps.FindIndex(s => s.Wait?.Any(c => c.Pattern == "^..05$") == true);
    Assert.Equal(firstRouteCheck + 1, sequence.WaypointEnteredAfter[0]);
  }
}

