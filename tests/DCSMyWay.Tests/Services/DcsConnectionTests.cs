using System.Net;
using System.Net.Sockets;
using System.Text;
using DCSMyWay.Models;
using DCSMyWay.Services;

namespace DCSMyWay.Tests.Services;

public class DcsConnectionTests
{
  [Fact]
  public async Task ListenerKeepsTheLatestStateSentByDcs()
  {
    using var listener = new DcsListener(0);
    Assert.True(listener.TryStart());
    using var dcs = new UdpClient();

    var message = """{"type":"camera","model":"F-16C_50","lat":41.9,"lon":43.7,"elev":1677.5,"crosshair":true,"busy":false,"aircraftId":16777472}""";
    await dcs.SendAsync(Encoding.UTF8.GetBytes(message), new IPEndPoint(IPAddress.Loopback, listener.Port), TestContext.Current.CancellationToken);
    await WaitUntilAsync(() => listener.IsConnected);

    Assert.Equal(new DcsState(new DcsPosition("F-16C_50", 41.9, 43.7, 1677.5), true, false, 16777472, null, null, 0), listener.Latest);
  }

  [Fact]
  public async Task ListenerRaisesMarkWhenTheAcceptButtonIsClickedInDcs()
  {
    using var listener = new DcsListener(0);
    Assert.True(listener.TryStart());
    var marks = new List<DcsPosition>();
    listener.MarkReceived += (_, position) => { lock (marks) marks.Add(position); };
    using var dcs = new UdpClient();
    var endpoint = new IPEndPoint(IPAddress.Loopback, listener.Port);

    var camera = """{"type":"camera","model":"F-16C_50","lat":1,"lon":2,"elev":3,"crosshair":true,"busy":false}""";
    var mark = """{"type":"mark","model":"F-16C_50","lat":41.9,"lon":43.7,"elev":1677.5,"crosshair":true,"busy":false}""";
    await dcs.SendAsync(Encoding.UTF8.GetBytes(camera), endpoint, TestContext.Current.CancellationToken);
    await dcs.SendAsync(Encoding.UTF8.GetBytes(mark), endpoint, TestContext.Current.CancellationToken);
    await WaitUntilAsync(() => { lock (marks) return marks.Count > 0; });

    lock (marks) Assert.Equal([new DcsPosition("F-16C_50", 41.9, 43.7, 1677.5)], marks);
  }

  [Fact]
  public void ListenerIsNotConnectedUntilDcsSendsSomething()
  {
    using var listener = new DcsListener(0);
    listener.TryStart();

    Assert.False(listener.IsConnected);
    Assert.Null(listener.Latest);
  }

  [Fact]
  public void ListenerReportsWhenThePortIsAlreadyInUse()
  {
    using var theWay = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    var busyPort = ((IPEndPoint)theWay.Client.LocalEndPoint!).Port;
    using var listener = new DcsListener(busyPort);

    Assert.False(listener.TryStart());
    Assert.False(listener.IsListening);
  }

  [Fact]
  public async Task SenderWritesTheCommandAsASingleLine()
  {
    var dcs = new TcpListener(IPAddress.Loopback, 0);
    dcs.Start();
    try
    {
      var sender = new DcsSender(((IPEndPoint)dcs.LocalEndpoint).Port);
      var accept = dcs.AcceptTcpClientAsync(TestContext.Current.CancellationToken);

      var sent = await sender.SetCrosshairVisibleAsync(true);
      using var client = await accept;
      using var reader = new StreamReader(client.GetStream());
      var received = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

      Assert.True(sent);
      Assert.Equal(DcsProtocol.CreateCrosshairCommand(true) + "\n", received);
    }
    finally
    {
      dcs.Stop();
    }
  }

  [Fact]
  public async Task SenderReportsWhenDcsIsNotListening()
  {
    var probe = new TcpListener(IPAddress.Loopback, 0);
    probe.Start();
    var closedPort = ((IPEndPoint)probe.LocalEndpoint).Port;
    probe.Stop();

    var sent = await new DcsSender(closedPort).SetCrosshairVisibleAsync(true);

    Assert.False(sent);
  }

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(20, TestContext.Current.CancellationToken);
  }
}
