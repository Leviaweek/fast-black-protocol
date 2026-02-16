using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using BlackFastProtocol.Package;

namespace BlackFastProtocol;

public sealed class BlackFastListener(IPEndPoint endPoint): IDisposable
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
            var header = ReadHeader(owner);

            var package = new UdpPackage(header, owner, length);

            if (_clients.TryGetValue(header.SessionId, out var client))
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

            client = new BlackFastServerClient(_client, remoteEndpoint, channel, header.SessionId,() =>
            {
                _clients.TryRemove(header.SessionId, out _);
                channel.Writer.TryComplete();
            }, token);

            if (_clients.TryAdd(header.SessionId, client))
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
    
    public Task StartAsync(CancellationToken token) => ReceiveLoop(token);

    private static PackageHeader ReadHeader(IMemoryOwner<byte> owner) => PackageHeader.ReadPackage(owner.Memory);

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

public sealed class FastBlackSessionContext(BlackFastClient client, Guid sessionId)
{
    public BlackFastClient Session { get; } = client;
    public bool IsAborted { get; set; }

    public bool IsHandshake { get; set; }
    public PackageBase? LastReceivedPackage { get; set; }
    public IWriteablePackage? LastSentPackage { get; set; }
    
    private int _currentSequence = int.MinValue;
    public int CurrentSequence => _currentSequence;
    
    public int GetNextSequence() => Interlocked.Increment(ref _currentSequence);
    public Guid SessionId { get; } = sessionId;
}