using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using BlackFastProtocol.Package;
using BlackFastProtocol.Package.Handshake;

namespace BlackFastProtocol;

public sealed class BlackFastListener(IPEndPoint endPoint): IDisposable
{
    private readonly Dictionary<PackageType, Func<ReadOnlyMemory<byte>, IPackageBody>> _bodyReaders = new() {
        [PackageType.Handshake] = buffer => HandshakeBody.ReadPackage(buffer),
    };


    private readonly UdpClient _client = new(endPoint);
    
    private readonly ConcurrentDictionary<Guid, BlackFastServerClient> _clients = new();
    
    private readonly Channel<BlackFastServerClient> _uniqueClients = Channel.CreateUnbounded<BlackFastServerClient>();

    public async Task<BlackFastClient> AcceptClientAsync(CancellationToken token)
    {
        var client = await _uniqueClients.Reader.ReadAsync(token);
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
            var header = PackageHeader.ReadPackage(memory);
            var body = _bodyReaders[header.Type](memory[header.Length..length]);
            var package = new ProtocolPackage(header, body);
            
            if (_clients.TryGetValue(header.SessionId, out var client))
            {
                client.DataChannel.Writer.TryWrite(package);
                client.UpdateEndpoint(remoteEndpoint);
                continue;
            }

            var channel = Channel.CreateUnbounded<ProtocolPackage>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            var sessionId = header.SessionId;
            client = new BlackFastServerClient(_client, remoteEndpoint, channel, header.SessionId,() =>
            {
                _clients.TryRemove(sessionId, out _);
            }, token);

            if (_clients.TryAdd(header.SessionId, client))
            {
                await _uniqueClients.Writer.WriteAsync(client, token);
                await channel.Writer.WriteAsync(package, token);
            }
            else
            {
                client.Dispose();
            }
        }
    }
    
    public Task StartAsync(CancellationToken token) => ReceiveLoop(token);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _client.Dispose();
    }
    
    ~BlackFastListener()
    {
        Dispose();
    }
}