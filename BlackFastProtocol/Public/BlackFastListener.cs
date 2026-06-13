using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using BlackFastProtocol.Internal.Package;

namespace BlackFastProtocol.Public;

public sealed class BlackFastListener(IPEndPoint endPoint) : IDisposable, IAsyncDisposable
{
    private readonly UdpClient _client = new(endPoint);
    private readonly ConcurrentDictionary<Guid, BlackFastServerClient> _clients = new();
    private readonly Channel<BlackFastServerClient> _uniqueClients =
        Channel.CreateUnbounded<BlackFastServerClient>();
    private CancellationTokenSource? _runCts;
    private Task? _receiveTask;
    private int _disposed;

    public IPEndPoint EndPoint => _client.Client.LocalEndPoint as IPEndPoint
        ?? throw new InvalidOperationException("LocalEndPoint is not an IPEndPoint");

    public async Task<BlackFastClient> AcceptClientAsync(CancellationToken token)
    {
        // Packets arrive before StartAsync — they are buffered in ReorderingBuffer
        // and drained inside StartAsync, so the ordering guarantee is preserved.
        var client = await _uniqueClients.Reader.ReadAsync(token);
        await client.StartAsync(token);
        return client;
    }

    private async Task ReceiveLoop(CancellationToken token)
    {
        var emptyEndpoint = new IPEndPoint(IPAddress.Any, 0);
        var buffer = new byte[BlackFastClient.MaxPackageSize];
        var memory = buffer.AsMemory();

        while (!token.IsCancellationRequested)
        {
            var result = await _client.Client.ReceiveFromAsync(memory, SocketFlags.None, emptyEndpoint, token);
            var length = result.ReceivedBytes;

            if (length < PackageHeader.Size) continue;

            var remoteEndpoint = (IPEndPoint)result.RemoteEndPoint;
            if (!PackageHelper.TryReadPackage(memory[..length], out var package))
                continue;

            var header = package!.Header;

            if (_clients.TryGetValue(header.SessionId, out var client))
            {
                await client.ReadPackageAsync(package, token);
                client.UpdateEndpoint(remoteEndpoint);
                continue;
            }

            var sessionId = header.SessionId;
            client = new BlackFastServerClient(_client, remoteEndpoint, sessionId,
                () => _clients.TryRemove(sessionId, out _));

            if (_clients.TryAdd(sessionId, client))
            {
                await _uniqueClients.Writer.WriteAsync(client, token);
                await client.ReadPackageAsync(package, token);
            }
            else
            {
                await client.DisposeAsync();
            }
        }
    }

    public Task StartAsync(CancellationToken token)
    {
        if (_receiveTask is not null) return _receiveTask;

        _runCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _receiveTask = ReceiveLoop(_runCts.Token);
        return _receiveTask;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _uniqueClients.Writer.TryComplete();
        _runCts?.Cancel();

        foreach (var client in _clients.Values)
            client.Dispose();

        _client.Dispose();
        _runCts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _uniqueClients.Writer.TryComplete();

        if (_runCts is not null)
            await _runCts.CancelAsync();

        foreach (var client in _clients.Values)
            await client.DisposeAsync();

        _client.Dispose();

        if (_receiveTask is not null)
        {
            try { await _receiveTask; }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException) { }
        }

        _runCts?.Dispose();
    }
}
