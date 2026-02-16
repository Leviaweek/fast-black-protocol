using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using BlackFastProtocol.Package;
using BlackFastProtocol.Package.DataPackage;
using BlackFastProtocol.Package.Handshake;

namespace BlackFastProtocol;

public sealed class BlackFastServerClient : BlackFastClient, IDisposable
{
    private readonly Dictionary<PackageType, IPackageHandler> _handlers = new() {
        [PackageType.Handshake] = new PackageHandlerAdapter<HandshakePackage>(new HandshakePackageHandler()),
        
    };

    private volatile IPEndPoint _remoteEndPoint;
    internal readonly Channel<UdpPackage> DataChannel;
    private readonly Action _dispose;
    private readonly FastBlackSessionContext _context;

    public BlackFastServerClient(UdpClient client,
        IPEndPoint remoteEndPoint,
        Channel<UdpPackage> dataChannel,
        Guid sessionId,
        Action dispose, CancellationToken cancellationToken) : base(client)
    {
        _dispose = dispose;
        _remoteEndPoint = remoteEndPoint;
        DataChannel = dataChannel;
        _context = new FastBlackSessionContext(this, sessionId);
        _ = ReadPackagesAsync(cancellationToken);
    }

    public override IPEndPoint EndPoint => _remoteEndPoint;

    internal void UpdateEndpoint(IPEndPoint remoteEndPoint)
    {
        Interlocked.Exchange(ref _remoteEndPoint, remoteEndPoint);
    }

    private async Task ReadPackagesAsync(CancellationToken cancellationToken)
    {
        await foreach (var udpPackage in DataChannel.Reader.ReadAllAsync(cancellationToken))
        {
            using (udpPackage)
            {
                await _handlers[udpPackage.Header.Type].HandlePackageAsync(udpPackage.Data, _context, cancellationToken);

                if (!_context.IsAborted) continue;
                Dispose();
                break;
            }

        }
    }

    public override async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var nextSequence = _context.GetNextSequence();
        var dataPackage = new DataPackage(_context.SessionId, nextSequence, buffer);
        await SendAsync(dataPackage, cancellationToken);
    }

    public override void Send(ReadOnlyMemory<byte> buffer)
    {
        var nextSequence = _context.GetNextSequence();
        var dataPackage = new DataPackage(_context.SessionId, nextSequence, buffer);
        Send(dataPackage);
    }

    internal override void Send<T>(T dataPackage)
    {
        Span<byte> packageBuffer = stackalloc byte[dataPackage.Length];

        dataPackage.ToBytes(packageBuffer);
        Client.Send(packageBuffer, _remoteEndPoint);
    }

    internal override async ValueTask SendAsync<T>(T dataPackage, CancellationToken cancellationToken)
    {
        using var packageBufferOwner = MemoryPool<byte>.Shared.Rent(dataPackage.Length);

        var packageBuffer = packageBufferOwner.Memory[..dataPackage.Length];

        dataPackage.ToBytes(packageBuffer.Span);
        await Client.SendAsync(packageBuffer, _remoteEndPoint, cancellationToken);
    }


    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _dispose();
    }

    ~BlackFastServerClient()
    {
        Dispose();
    }
}