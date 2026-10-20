using System.Net;
using System.Net.Sockets;
using System.Text;
using DCSMyWay.Models;

namespace DCSMyWay.Services;

public sealed class DcsSender(int hookPort, int cockpitPort)
{
  public const int DefaultHookPort = 42081;
  public const int DefaultCockpitPort = 42082;

  private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(2);

  public Task<bool> SendWaypointsAsync(int transferId, IEnumerable<ButtonPress> presses) =>
    SendAsync(cockpitPort, DcsProtocol.CreateWaypointsCommand(transferId, presses));

  public Task<bool> CancelWaypointsAsync() =>
    SendAsync(cockpitPort, DcsProtocol.CreateCancelCommand());

  public Task<bool> SetCrosshairVisibleAsync(bool visible) =>
    SendAsync(hookPort, DcsProtocol.CreateCrosshairCommand(visible));

  public Task<bool> ShowStatusAsync(string text) =>
    SendAsync(hookPort, DcsProtocol.CreateStatusCommand(text));

  public Task<bool> ShowRouteAsync(IEnumerable<Waypoint> waypoints) =>
    SendAsync(hookPort, DcsProtocol.CreateRouteCommand(waypoints));

  private static async Task<bool> SendAsync(int port, string command)
  {
    using var cancellation = new CancellationTokenSource(SendTimeout);
    try
    {
      using var client = new TcpClient();
      await client.ConnectAsync(IPAddress.Loopback, port, cancellation.Token);
      await client.GetStream().WriteAsync(Encoding.UTF8.GetBytes(command + "\n"), cancellation.Token);
      return true;
    }
    catch (Exception e) when (e is SocketException or OperationCanceledException)
    {
      return false;
    }
  }
}
