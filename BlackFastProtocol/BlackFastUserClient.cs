using System.Buffers;
using System.Net;
using System.Net.Sockets;
using BlackFastProtocol.Package;
using BlackFastProtocol.Package.DataPackage;

namespace BlackFastProtocol;

public sealed class BlackFastUserClient : BlackFastClient
{
    private readonly FastBlackSessionContext _context;

    public BlackFastUserClient(IPEndPoint endPoint) : base(new UdpClient(endPoint))
    {
        EndPoint = endPoint;
        _context = new FastBlackSessionContext(this, Guid.NewGuid());
    }

    public void Connect(IPEndPoint endPoint) => Client.Connect(endPoint);

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
        var buffer = ArrayPool<byte>.Shared.Rent(package.Length);
        var span = buffer.AsSpan();
        package.Header.WriteData(span);
        package.Body.WriteData(span[package.Header.Length..]);
        Client.Send(span);
        ArrayPool<byte>.Shared.Return(buffer);
    }

    internal override async ValueTask SendAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(package.Length);
        var span = buffer.AsSpan();
        package.Header.WriteData(span);
        package.Body.WriteData(span[package.Header.Length..]);
        await Client.SendAsync(buffer, cancellationToken);
        ArrayPool<byte>.Shared.Return(buffer);
    }

    public override IPEndPoint EndPoint { get; }
}