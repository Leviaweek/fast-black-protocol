using System.Buffers;
using System.Net;
using System.Net.Sockets;
using BlackFastProtocol.Package;
using BlackFastProtocol.Package.DataPackage;

namespace BlackFastProtocol;

public sealed class BlackFastServerClient : BlackFastClient, IDisposable
{
    private volatile IPEndPoint _remoteEndPoint;
    private readonly Action _dispose;
    private readonly FastBlackSessionContext _context;
    private bool _isStarted;

    public BlackFastServerClient(UdpClient client,
        IPEndPoint remoteEndPoint,
        Guid sessionId,
        Action dispose) : base(client)
    {
        _dispose = dispose;
        _remoteEndPoint = remoteEndPoint;
        EndPoint = client.Client.LocalEndPoint as IPEndPoint ?? throw new InvalidOperationException("LocalEndPoint is not an IPEndPoint");
        _context = new FastBlackSessionContext(this, sessionId);
    }

    internal void Start() => _isStarted = true;


    public override IPEndPoint EndPoint { get; }

    internal void UpdateEndpoint(IPEndPoint remoteEndPoint)
    {
        Interlocked.Exchange(ref _remoteEndPoint, remoteEndPoint);
    }

    internal Task ReadPackageAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        if (!_isStarted)
        {
            return Task.CompletedTask;
        }

        return _context.HandlePackageAsync(package, cancellationToken);
    }

    public override async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var protocolPackage = GetProtocolPackage(buffer);
        await SendAsync(protocolPackage, cancellationToken);
    }

    public override void Send(ReadOnlyMemory<byte> buffer)
    {
        var protocolPackage = GetProtocolPackage(buffer);
        Send(protocolPackage);
    }

    private ProtocolPackage GetProtocolPackage(ReadOnlyMemory<byte> buffer)
    {
        var header = PackageHeader.CreateFromContext(_context, PackageType.DataPackage);
        var dataPackage = new DataPackageBody(buffer);
        var protocolPackage = new ProtocolPackage(header, dataPackage);
        return protocolPackage;
    }

    internal override void Send(ProtocolPackage package)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(package.Length);
        var span = buffer.AsSpan();
        package.Header.WriteData(span);
        package.Body.WriteData(span[package.Header.Length..]);
        Client.Send(span, _remoteEndPoint);
        ArrayPool<byte>.Shared.Return(buffer);
        _context.LastSentPackage = package;
    }

    internal override async ValueTask SendAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(package.Length);
        var span = buffer.AsSpan();
        package.Header.WriteData(span);
        package.Body.WriteData(span[package.Header.Length..]);
        await Client.SendAsync(buffer, _remoteEndPoint, cancellationToken);
        ArrayPool<byte>.Shared.Return(buffer);
        _context.LastSentPackage = package;
    }

    public byte[] ReceiveAsync(CancellationToken cancellationToken)
    {
        //ToDo
        return [];
    }


    public void Dispose()
    {
        _dispose();
        _context.Dispose();
    }
}