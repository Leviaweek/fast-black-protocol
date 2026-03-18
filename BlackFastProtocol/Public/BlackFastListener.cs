using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using BlackFastProtocol.Internal.Package;

namespace BlackFastProtocol.Public;

public sealed class BlackFastListener(IPEndPoint endPoint): IDisposable
{
    private readonly UdpClient _client = new(endPoint);
    
    private readonly ConcurrentDictionary<Guid, BlackFastServerClient> _clients = new();
    
    private readonly Channel<BlackFastServerClient> _uniqueClients = Channel.CreateUnbounded<BlackFastServerClient>();

    public async Task<BlackFastClient> AcceptClientAsync(CancellationToken token)
    {
        var client = await _uniqueClients.Reader.ReadAsync(token);
        await client.StartAsync(token);
        return client;
    }

    private async Task ReceiveLoop(CancellationToken token)
    {
        var emptyEndpoint = new IPEndPoint(IPAddress.Any, 0);
        var buffer = new byte[65535];
        var memory = buffer.AsMemory();
        while (!token.IsCancellationRequested)
        {
            var result = await _client.Client.ReceiveFromAsync(memory, SocketFlags.None, emptyEndpoint, token);

            var length = result.ReceivedBytes;

            if (length < 21)
            {
                continue;
            }

            var remoteEndpoint = (IPEndPoint)result.RemoteEndPoint;
            var header = PackageHeader.ReadData(memory);
            var body = PackageHelper.BodyReaders[header.Type](memory[header.Length..length]);
            var package = new ProtocolPackage(header, body);
            
            if (_clients.TryGetValue(header.SessionId, out var client))
            {
                _ = client.ReadPackageAsync(package, token);
                client.UpdateEndpoint(remoteEndpoint);
                continue;
            }

            var sessionId = header.SessionId;
            client = new BlackFastServerClient(_client, remoteEndpoint, header.SessionId,() =>
            {
                _clients.TryRemove(sessionId, out _);
            });

            if (_clients.TryAdd(header.SessionId, client))
            {
                await _uniqueClients.Writer.WriteAsync(client, token);
                _ = client.ReadPackageAsync(package, token);
            }
            else
            {
                client.Dispose();
            }
        }
    }

    public Task StartAsync(CancellationToken token)
    {
        return ReceiveLoop(token);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}

//adapter(memory)
//innerHandler<T>

//T.ReadPackage(memory)
//innerHandler<T>(package)