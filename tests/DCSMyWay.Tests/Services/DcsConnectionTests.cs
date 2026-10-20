using System.Net;
using System.Net.Sockets;
using System.Text;
using DCSMyWay.Models;
using DCSMyWay.Services;

namespace DCSMyWay.Tests.Services;

public class DcsConnectionTests
{
  private const string Camera = """{"type":"camera","model":"F-16C_50","lat":41.9,"lon":43.7,"elev":1677.5,"crosshair":true,"aircraftId":16777472}""";
  private const string Cockpit = """{"type":"cockpit","model":"F-16C_50","steerpoint":3,"busy":true,"transferId":7,"done":42}""";

  [Fact]
  public async Task ListenerKeepsTheLatestCameraSentByTheHook()
  {
    using var listener = new DcsListener(0);
    Assert.True(listener.TryStart());
    using var dcs = new UdpClient();

    await SendAsync(dcs, listener, Camera);
    await WaitUntilAsync(() => listener.IsConnected);

    Assert.Equal(new DcsState(new DcsPosition("F-16C_50", 41.9, 43.7, 1677.5), true, false, 16777472, null, null, 0), listener.Latest);
    Assert.False(listener.IsCockpitConnected);
  }

  [Fact]
  public async Task ListenerAddsTheCockpitStateSentByTheExportScript()
  {
    using var listener = new DcsListener(0);
    Assert.True(listener.TryStart());
    using var dcs = new UdpClient();

    await SendAsync(dcs, listener, Camera);
    await SendAsync(dcs, listener, Cockpit);
    await WaitUntilAsync(() => listener.IsConnected && listener.IsCockpitConnected);

    Assert.Equal(new DcsState(new DcsPosition("F-16C_50", 41.9, 43.7, 1677.5), true, true, 16777472, 3, 7, 42), listener.Latest);
  }

  [Fact]
  public async Task CockpitStateAloneIsNotAConnectionToDcs()
  {
    using var listener = new DcsListener(0);
    Assert.True(listener.TryStart());
    using var dcs = new UdpClient();

    await SendAsync(dcs, listener, Cockpit);
    await WaitUntilAsync(() => listener.IsCockpitConnected);

    Assert.True(listener.IsCockpitConnected);
    Assert.False(listener.IsConnected);
    Assert.Null(listener.Latest);
  }

  [Fact]
  public async Task ListenerRaisesMarkWhenTheAcceptButtonIsClickedInDcs()
  {
    using var listener = new DcsListener(0);
    Assert.True(listener.TryStart());
    var marks = new List<DcsPosition>();
    listener.MarkReceived += (_, position) => { lock (marks) marks.Add(position); };
    using var dcs = new UdpClient();

    await SendAsync(dcs, listener, """{"type":"camera","model":"F-16C_50","lat":1,"lon":2,"elev":3,"crosshair":true}""");
    await SendAsync(dcs, listener, Cockpit);
    await SendAsync(dcs, listener, """{"type":"mark","model":"F-16C_50","lat":41.9,"lon":43.7,"elev":1677.5,"crosshair":true}""");
    await WaitUntilAsync(() => { lock (marks) return marks.Count > 0; });

    lock (marks) Assert.Equal([new DcsPosition("F-16C_50", 41.9, 43.7, 1677.5)], marks);
  }

  [Fact]
  public void ListenerIsNotConnectedUntilDcsSendsSomething()
  {
    using var listener = new DcsListener(0);
    listener.TryStart();

    Assert.False(listener.IsConnected);
    Assert.False(listener.IsCockpitConnected);
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
  public async Task SenderSendsHookCommandsToTheHookAndCockpitCommandsToTheExportScript()
  {
    var hook = new TcpListener(IPAddress.Loopback, 0);
    var cockpit = new TcpListener(IPAddress.Loopback, 0);
    hook.Start();
    cockpit.Start();
    try
    {
      var sender = new DcsSender(((IPEndPoint)hook.LocalEndpoint).Port, ((IPEndPoint)cockpit.LocalEndpoint).Port);

      Assert.True(await sender.SetCrosshairVisibleAsync(true));
      Assert.Equal(DcsProtocol.CreateCrosshairCommand(true) + "\n", await ReadCommandAsync(hook));
      Assert.True(await sender.CancelWaypointsAsync());
      Assert.Equal(DcsProtocol.CreateCancelCommand() + "\n", await ReadCommandAsync(cockpit));
      CockpitStep[] steps = [new(new(17, 3032, 150, -1))];
      Assert.True(await sender.SendWaypointsAsync(3, steps));
      Assert.Equal(DcsProtocol.CreateWaypointsCommand(3, steps) + "\n", await ReadCommandAsync(cockpit));
    }
    finally
    {
      hook.Stop();
      cockpit.Stop();
    }
  }

  [Fact]
  public async Task SenderReportsWhenDcsIsNotListening()
  {
    var probe = new TcpListener(IPAddress.Loopback, 0);
    probe.Start();
    var closedPort = ((IPEndPoint)probe.LocalEndpoint).Port;
    probe.Stop();
    var sender = new DcsSender(closedPort, closedPort);

    Assert.False(await sender.SetCrosshairVisibleAsync(true));
    Assert.False(await sender.CancelWaypointsAsync());
  }

  private static async Task SendAsync(UdpClient dcs, DcsListener listener, string message) =>
    await dcs.SendAsync(Encoding.UTF8.GetBytes(message), new IPEndPoint(IPAddress.Loopback, listener.Port), TestContext.Current.CancellationToken);

  private static async Task<string> ReadCommandAsync(TcpListener listener)
  {
    using var client = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
    using var reader = new StreamReader(client.GetStream());
    return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
  }

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(20, TestContext.Current.CancellationToken);
  }
}
