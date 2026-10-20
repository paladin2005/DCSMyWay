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
  private DcsState? _latest;
  private long _lastReceivedTicks;

  public event EventHandler<DcsPosition>? MarkReceived;

  public int Port { get; private set; } = port;

  public bool IsListening => _client is not null;

  public DcsState? Latest => Volatile.Read(ref _latest);

  public DateTimeOffset LastReceivedUtc => new(Interlocked.Read(ref _lastReceivedTicks), TimeSpan.Zero);

  public bool IsConnected => DateTimeOffset.UtcNow - LastReceivedUtc < ConnectionTimeout;

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

  private async Task ReceiveAsync(UdpClient client, CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        var result = await client.ReceiveAsync(cancellationToken);
        if (!DcsProtocol.TryParseMessage(result.Buffer, out var message)) continue;
        Volatile.Write(ref _latest, message.State);
        Interlocked.Exchange(ref _lastReceivedTicks, DateTimeOffset.UtcNow.UtcTicks);
        if (message.Type == DcsMessageType.Mark) MarkReceived?.Invoke(this, message.State.Position);
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
