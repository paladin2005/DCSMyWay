using System.Net;
using System.Net.Sockets;
using DCSMyWay.Models;

namespace DCSMyWay.Services;

public sealed class DcsListener(int port) : IDisposable
{
  public const int DefaultPort = 42080;

  private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(2);

  private readonly CancellationTokenSource _cancellation = new();
  private UdpClient? _client;
  private CameraState? _camera;
  private CockpitState? _cockpit;
  private long _cameraReceivedTicks;
  private long _cockpitReceivedTicks;

  public event EventHandler<DcsPosition>? MarkReceived;

  public int Port { get; private set; } = port;

  public bool IsListening => _client is not null;

  public bool IsConnected => IsRecent(ref _cameraReceivedTicks);

  public bool IsCockpitConnected => IsRecent(ref _cockpitReceivedTicks);

  public DcsState? Latest
  {
    get
    {
      var camera = Volatile.Read(ref _camera);
      if (camera is null) return null;
      var cockpit = IsCockpitConnected ? Volatile.Read(ref _cockpit) : null;
      return new DcsState(
        camera.Position,
        camera.CrosshairVisible,
        cockpit?.IsEnteringWaypoints == true,
        camera.AircraftId,
        cockpit?.ActiveSteerpoint,
        cockpit?.TransferId,
        cockpit?.PressesDone ?? 0,
        cockpit?.TransferFailure);
    }
  }

  public bool TryStart()
  {
    if (_client is not null) return true;
    try
    {
      _client = new UdpClient(new IPEndPoint(IPAddress.Loopback, Port));
    }
    catch (SocketException)
    {
      return false;
    }
    Port = ((IPEndPoint)_client.Client.LocalEndPoint!).Port;
    _ = ReceiveAsync(_client, _cancellation.Token);
    return true;
  }

  public void Dispose()
  {
    _cancellation.Cancel();
    _client?.Dispose();
  }

  private static bool IsRecent(ref long ticks) =>
    DateTimeOffset.UtcNow - new DateTimeOffset(Interlocked.Read(ref ticks), TimeSpan.Zero) < ConnectionTimeout;

  private async Task ReceiveAsync(UdpClient client, CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        var result = await client.ReceiveAsync(cancellationToken);
        if (!DcsProtocol.TryParseMessage(result.Buffer, out var message)) continue;
        if (message.Cockpit is { } cockpit)
        {
          Volatile.Write(ref _cockpit, cockpit);
          Interlocked.Exchange(ref _cockpitReceivedTicks, DateTimeOffset.UtcNow.UtcTicks);
        }

        if (message.Camera is not { } camera) continue;
        Volatile.Write(ref _camera, camera);
        Interlocked.Exchange(ref _cameraReceivedTicks, DateTimeOffset.UtcNow.UtcTicks);
        if (message.Type == DcsMessageType.Mark) MarkReceived?.Invoke(this, camera.Position);
      }
      catch (OperationCanceledException)
      {
        return;
      }
      catch (ObjectDisposedException)
      {
        return;
      }
      catch (SocketException)
      {
      }
    }
  }
}
