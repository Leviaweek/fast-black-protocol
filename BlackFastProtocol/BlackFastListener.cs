using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using BlackFastProtocol.Package;

namespace BlackFastProtocol;

public sealed class BlackFastListener(IPEndPoint endPoint)
{
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
        while (!token.IsCancellationRequested)
        {
            var owner = MemoryPool<byte>.Shared.Rent(65535);
            var result = await _client.Client.ReceiveFromAsync(owner.Memory, SocketFlags.None, emptyEndpoint, token);

            var length = result.ReceivedBytes;

            if (length < 16)
            {
                owner.Dispose();
                continue;
            }

            var remoteEndpoint = (IPEndPoint)result.RemoteEndPoint;
            var id = ReadUserId(owner);

            var package = new UdpPackage(owner, length);

            if (_clients.TryGetValue(id, out var client))
            {
                client.UpdateEndpoint(remoteEndpoint);

                if (!client.DataChannel.Writer.TryWrite(package))
                {
                    package.Dispose();
                }
                continue;
            }

            var channel = Channel.CreateUnbounded<UdpPackage>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            client = new BlackFastServerClient(_client, remoteEndpoint, channel,() =>
            {
                _clients.TryRemove(id, out _);
                channel.Writer.TryComplete();
            });

            if (_clients.TryAdd(id, client))
            {
                await _uniqueClients.Writer.WriteAsync(client, token);
                await channel.Writer.WriteAsync(package, token);
            }
            else
            {
                package.Dispose();
                client.Dispose();
            }
        }
    }

    private static Guid ReadUserId(IMemoryOwner<byte> owner)
    {
        return new Guid(owner.Memory.Span[..16]);
    }
}

public sealed class FastBlackSessionContext(BlackFastClient client)
{
    public BlackFastClient Session { get; } = client;
    public bool IsAborted { get; set; }

    public bool IsHandshaked { get; set; }
    public PackageBase? LastReceivedPackage { get; set; }
    public IWriteablePackage? LastSentPackage { get; set; }
    public int CurrentSequence { get; set; }
}