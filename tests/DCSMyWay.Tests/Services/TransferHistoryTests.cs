using DCSMyWay.Models;
using DCSMyWay.Services;

namespace DCSMyWay.Tests.Services;

public class TransferHistoryTests
{
  private static readonly Waypoint W1 = new(1, "W1", 41.9, 43.7, 1677.5);
  private static readonly Waypoint W2 = new(2, "W2", 42.1, 43.9, 250);
  private static readonly Waypoint W3 = new(3, "W3", 42.3, 44.1, 12);

  [Fact]
  public void FirstTransferIsNotARepeat()
  {
    Assert.False(new TransferHistory().IsRepeat(16777472, [W1, W2]));
  }

  [Fact]
  public void SameAircraftAndSameRouteIsARepeat()
  {
    var history = new TransferHistory();

    history.Record(16777472, [W1, W2]);

    Assert.True(history.IsRepeat(16777472, [W1, W2]));
  }

  [Fact]
  public void NewAircraftAfterRespawnIsNotARepeat()
  {
    var history = new TransferHistory();

    history.Record(16777472, [W1, W2]);

    Assert.False(history.IsRepeat(16777999, [W1, W2]));
  }

  [Theory]
  [InlineData(new[] { 1, 2, 3 })]
  [InlineData(new[] { 2 })]
  [InlineData(new[] { 2, 1 })]
  public void ChangedRouteIsNotARepeat(int[] route)
  {
    var history = new TransferHistory();
    Waypoint[] all = [W1, W2, W3];

    history.Record(16777472, [W1, W2]);

    Assert.False(history.IsRepeat(16777472, route.Select(n => all[n - 1])));
  }

  [Fact]
  public void ClearForgetsTheLastTransfer()
  {
    var history = new TransferHistory();
    history.Record(16777472, [W1, W2]);

    history.Clear();

    Assert.False(history.IsRepeat(16777472, [W1, W2]));
  }

  [Fact]
  public void UnknownAircraftWithTheSameRouteIsTreatedAsARepeat()
  {
    var history = new TransferHistory();

    history.Record(null, [W1]);

    Assert.True(history.IsRepeat(null, [W1]));
  }

  [Fact]
  public void RecordKeepsACopyOfTheRoute()
  {
    var history = new TransferHistory();
    var route = new List<Waypoint> { W1, W2 };

    history.Record(16777472, route);
    route.Add(W3);

    Assert.True(history.IsRepeat(16777472, [W1, W2]));
  }
}
