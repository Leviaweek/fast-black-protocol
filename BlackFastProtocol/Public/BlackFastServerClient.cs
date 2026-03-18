using System.Buffers;
using System.Net;
using System.Net.Sockets;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.DataPackage;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Public;

public sealed class BlackFastServerClient : BlackFastClient, IDisposable
{
    private volatile IPEndPoint _remoteEndPoint;
    private readonly Action _dispose;
    private readonly FastBlackSessionContext _context;

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

    internal Task StartAsync(CancellationToken cancellationToken) => _context.StartAsync(cancellationToken);


    public override IPEndPoint EndPoint { get; }

    internal void UpdateEndpoint(IPEndPoint remoteEndPoint)
    {
        Interlocked.Exchange(ref _remoteEndPoint, remoteEndPoint);
    }

    internal Task ReadPackageAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
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
        var span = buffer.AsSpan()[..package.Length];
        package.Header.WriteData(span);
        package.Body.WriteData(span[package.Header.Length..]);
        Client.Send(span, _remoteEndPoint);
        ArrayPool<byte>.Shared.Return(buffer);
        _context.Tracker.LastSentPackage = package;
    }

    internal override async ValueTask SendAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(package.Length);
        var memory = buffer.AsMemory()[..package.Length];
        var span = memory.Span;
        package.Header.WriteData(span);
        package.Body.WriteData(span[package.Header.Length..]);
        await Client.SendAsync(memory, _remoteEndPoint, cancellationToken);
        ArrayPool<byte>.Shared.Return(buffer);
        _context.Tracker.LastSentPackage = package;
    }

    public override async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        var result = await _context.DataChannel.Reader.ReadAsync(cancellationToken);
        return result;
    }

    public override byte[] Receive()
    {
        while (true)
        {
            if (_context.DataChannel.Reader.TryRead(out var result))
            {
                return result;
            }

            Task.Delay(30).GetAwaiter().GetResult();
        }
    }


    public void Dispose()
    {
        _dispose();
        _context.Dispose();
    }
}