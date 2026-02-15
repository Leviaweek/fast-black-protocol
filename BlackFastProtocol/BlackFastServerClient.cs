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
        Action dispose) : base(client)
    {
        _dispose = dispose;
        _remoteEndPoint = remoteEndPoint;
        DataChannel = dataChannel;
        _context = new FastBlackSessionContext(this);
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
                var span = udpPackage.Data.Span;
                var packageType = (PackageType)span[0];

                await _handlers[packageType].HandlePackageAsync(udpPackage.Data, _context, cancellationToken);

                if (!_context.IsAborted) continue;
                Dispose();
                break;
            }

        }
    }

    public override async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var dataPackage = new DataPackage(_context.CurrentSequence++, buffer);
        await SendAsync(dataPackage, cancellationToken);
    }

    private async Task SendAsync<T>(T dataPackage, CancellationToken cancellationToken)
        where T : PackageBase, IWriteablePackage
    {
        using var packageBufferOwner = MemoryPool<byte>.Shared.Rent(dataPackage.Length);

        var packageBuffer = packageBufferOwner.Memory[..dataPackage.Length];

        dataPackage.ToBytes(packageBuffer.Span);
        await Client.SendAsync(packageBuffer, _remoteEndPoint, cancellationToken);
    }

    public override void Send(ReadOnlyMemory<byte> buffer)
    {
        var dataPackage = new DataPackage(_context.CurrentSequence++, buffer);
        Send(dataPackage);
    }

    private void Send<T>(T dataPackage) 
        where T : PackageBase, IWriteablePackage
    {
        using var packageBufferOwner = MemoryPool<byte>.Shared.Rent(dataPackage.Length);

        var packageBuffer = packageBufferOwner.Memory[..dataPackage.Length];

        dataPackage.ToBytes(packageBuffer.Span);
        Client.Send(packageBuffer.Span, _remoteEndPoint);
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