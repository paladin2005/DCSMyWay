using DCSMyWay.Models;
using DCSMyWay.Services;

namespace DCSMyWay.Tests.Services;

public class TransferHistoryTests
{
  private const long Aircraft = 16777472;
  private const long NewAircraft = 16777999;
  private static readonly Waypoint W1 = new(1, "W1", 41.9, 43.7, 1677.5);
  private static readonly Waypoint W2 = new(2, "W2", 42.1, 43.9, 250);
  private static readonly Waypoint W3 = new(3, "W3", 42.3, 44.1, 12);
  private static readonly Waypoint W4 = new(4, "W4", 42.5, 44.3, 90);
  private static readonly int[] EnteredAfter = [30, 60, 90];

  private static TransferHistory Entered(int start, params Waypoint[] route)
  {
    var history = new TransferHistory();
    history.Record(Aircraft, 1, route, start, EnteredAfter);
    history.UpdateProgress(1, 90);
    return history;
  }

  [Fact]
  public void EverythingIsPendingBeforeTheFirstTransfer()
  {
    var pending = new TransferHistory().Pending(Aircraft, [W1, W2]);

    Assert.Equal([W1, W2], pending.Waypoints);
    Assert.Null(pending.StartSteerpoint);
  }

  [Fact]
  public void NothingIsEnteredWhenTheTransferStarts()
  {
    var history = new TransferHistory();

    history.Record(Aircraft, 1, [W1, W2, W3], 1, EnteredAfter);
    history.UpdateProgress(1, 0);

    Assert.False(history.IsEntered(Aircraft, W1));
  }

  [Fact]
  public void MarksEachWaypointWithItsSteerpointAsSoonAsItsPressesAreDone()
  {
    var history = new TransferHistory();
    history.Record(Aircraft, 1, [W1, W2, W3], 5, EnteredAfter);

    history.UpdateProgress(1, 29);
    Assert.False(history.IsEntered(Aircraft, W1));

    history.UpdateProgress(1, 30);
    Assert.Equal(5, history.SteerpointOf(Aircraft, W1));
    Assert.False(history.IsEntered(Aircraft, W2));

    history.UpdateProgress(1, 75);
    Assert.Equal(6, history.SteerpointOf(Aircraft, W2));
    Assert.False(history.IsEntered(Aircraft, W3));
  }

  [Fact]
  public void IgnoresProgressFromAnotherTransfer()
  {
    var history = new TransferHistory();
    history.Record(Aircraft, 2, [W1, W2, W3], 1, EnteredAfter);

    history.UpdateProgress(1, 90);
    history.UpdateProgress(null, 90);

    Assert.False(history.IsEntered(Aircraft, W1));
  }

  [Fact]
  public void NewWaypointsStartRightAfterTheLastEnteredSteerpoint()
  {
    var history = Entered(3, W1, W2);

    var pending = history.Pending(Aircraft, [W1, W2, W3, W4]);

    Assert.Equal([W3, W4], pending.Waypoints);
    Assert.Equal(5, pending.StartSteerpoint);
  }

  [Fact]
  public void ACancelledTransferResumesAtTheFirstWaypointThatDidNotGetIn()
  {
    var history = new TransferHistory();
    history.Record(Aircraft, 1, [W1, W2, W3], 1, EnteredAfter);
    history.UpdateProgress(1, 65);

    var pending = history.Pending(Aircraft, [W1, W2, W3]);

    Assert.Equal([W3], pending.Waypoints);
    Assert.Equal(3, pending.StartSteerpoint);
  }

  [Fact]
  public void NothingIsPendingWhenEveryWaypointIsEntered()
  {
    var pending = Entered(1, W1, W2).Pending(Aircraft, [W1, W2]);

    Assert.Empty(pending.Waypoints);
  }

  [Fact]
  public void DeletedWaypointsDoNotChangeWhereNewOnesGo()
  {
    var history = Entered(1, W1, W2, W3);

    var pending = history.Pending(Aircraft, [W1, W3, W4]);

    Assert.Equal([W4], pending.Waypoints);
    Assert.Equal(4, pending.StartSteerpoint);
  }

  [Fact]
  public void KeepsEnteredWaypointsAcrossTransfersToTheSameAircraft()
  {
    var history = Entered(1, W1, W2);

    history.Record(Aircraft, 2, [W3], 3, [30]);
    history.UpdateProgress(2, 30);

    Assert.Equal(1, history.SteerpointOf(Aircraft, W1));
    Assert.Equal(3, history.SteerpointOf(Aircraft, W3));
  }

  [Fact]
  public void TransferringAgainClearsTheMarksUntilEachWaypointIsEnteredAgain()
  {
    var history = Entered(1, W1, W2);

    history.Record(Aircraft, 2, [W1, W2], 1, [30, 60]);
    Assert.False(history.IsEntered(Aircraft, W1));

    history.UpdateProgress(2, 30);
    Assert.True(history.IsEntered(Aircraft, W1));
    Assert.False(history.IsEntered(Aircraft, W2));
  }

  [Fact]
  public void ANewAircraftStartsWithNothingEntered()
  {
    var history = Entered(1, W1, W2);

    Assert.False(history.IsEntered(NewAircraft, W1));
    Assert.Equal([W1, W2], history.Pending(NewAircraft, [W1, W2]).Waypoints);
    Assert.Null(history.Pending(NewAircraft, [W1, W2]).StartSteerpoint);
  }

  [Fact]
  public void TransferringToANewAircraftForgetsTheOldOne()
  {
    var history = Entered(1, W1);

    history.Record(NewAircraft, 2, [W2], 1, [30]);

    Assert.False(history.IsEntered(Aircraft, W1));
    Assert.False(history.IsEntered(NewAircraft, W1));
  }

  [Fact]
  public void RecordKeepsACopyOfTheRoute()
  {
    var history = new TransferHistory();
    var route = new List<Waypoint> { W1 };

    history.Record(Aircraft, 1, route, 1, EnteredAfter);
    route.Add(W2);
    history.UpdateProgress(1, 90);

    Assert.True(history.IsEntered(Aircraft, W1));
    Assert.False(history.IsEntered(Aircraft, W2));
  }

  [Fact]
  public void UnknownAircraftStillTracksEnteredWaypoints()
  {
    var history = new TransferHistory();

    history.Record(null, 1, [W1], 7, EnteredAfter);
    history.UpdateProgress(1, 30);

    Assert.Equal(7, history.SteerpointOf(null, W1));
  }

  [Fact]
  public void ClearForgetsEverything()
  {
    var history = Entered(1, W1, W2);

    history.Clear();

    Assert.False(history.IsEntered(Aircraft, W1));
    Assert.Null(history.Pending(Aircraft, [W1, W2]).StartSteerpoint);
  }
}
