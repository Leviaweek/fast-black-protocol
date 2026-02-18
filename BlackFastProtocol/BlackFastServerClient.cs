using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using BlackFastProtocol.Package;
using BlackFastProtocol.Package.DataPackage;
using BlackFastProtocol.Package.Handshake;

namespace BlackFastProtocol;

public sealed class BlackFastServerClient : BlackFastClient, IDisposable
{
    private readonly Dictionary<PackageType, IBodyHandler> _handlers = new() {
        [PackageType.Handshake] = new BodyHandlerAdapter<HandshakeBody>(new HandshakeBodyHandler()),
    };

    private volatile IPEndPoint _remoteEndPoint;
    internal readonly Channel<ProtocolPackage> DataChannel;
    private readonly Action _dispose;
    private readonly FastBlackSessionContext _context;

    public BlackFastServerClient(UdpClient client,
        IPEndPoint remoteEndPoint,
        Channel<ProtocolPackage> dataChannel,
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
        await foreach (var protoctolPackage in DataChannel.Reader.ReadAllAsync(cancellationToken))
        {
            await _handlers[protoctolPackage.Header.Type].HandlePackageAsync(protoctolPackage, _context, cancellationToken);

            if (!_context.IsAborted) continue;
            Dispose();
            break;
        }
    }

    public override async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var nextSequence = _context.GetNextSequence();
        var header = new PackageHeader(_context.SessionId, PackageType.DataPackage, nextSequence);
        var dataPackage = new DataPackageBody(buffer);
        var protocolPackage = new ProtocolPackage(header, dataPackage);
        await SendAsync(protocolPackage, cancellationToken);
    }

    public override void Send(ReadOnlyMemory<byte> buffer)
    {
        var nextSequence = _context.GetNextSequence();
        var header = new PackageHeader(_context.SessionId, PackageType.DataPackage, nextSequence);
        var dataPackage = new DataPackageBody(buffer);
        var protocolPackage = new ProtocolPackage(header, dataPackage);
        Send(protocolPackage);
    }

    internal override void Send(ProtocolPackage package)
    {
        var buffer = new byte[package.Length].AsSpan();
        package.Header.ToBytes(buffer);
        package.Body.ToBytes(buffer[package.Header.Length..]);
        Client.Send(buffer, _remoteEndPoint);
    }

    internal override async ValueTask SendAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        var buffer = new byte[package.Length].AsMemory();
        var span = buffer.Span;
        package.Header.ToBytes(span);
        package.Body.ToBytes(span[package.Header.Length..]);
        await Client.SendAsync(buffer, _remoteEndPoint, cancellationToken);
    }


    public void Dispose()
    {
        GC.SuppressFinalize(this);
        DataChannel.Writer.TryComplete();
        _dispose();
    }

    ~BlackFastServerClient()
    {
        Dispose();
    }
}