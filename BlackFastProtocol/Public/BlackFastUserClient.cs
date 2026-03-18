using System.Buffers;
using System.Net;
using System.Net.Sockets;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.DataPackage;
using BlackFastProtocol.Internal.Package.Handshake;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Public;

public sealed class BlackFastUserClient : BlackFastClient, IDisposable
{
    private readonly FastBlackSessionContext _context;
    private volatile uint _expectedSequence = uint.MinValue;

    public BlackFastUserClient(IPEndPoint endPoint) : base(new UdpClient(endPoint))
    {
        EndPoint = endPoint;
        _context = new FastBlackSessionContext(this, Guid.NewGuid());
    }

    public async Task ConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        Client.Connect(endPoint);
        var handshakeHeader = PackageHeader.CreateFromContext(_context, PackageType.Handshake);
        var handshakeBody = new HandshakeBody();
        var handshakePackage = new ProtocolPackage(handshakeHeader, handshakeBody);
        await SendAsync(handshakePackage, cancellationToken);
        _ = ReceiveLoop(cancellationToken);
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var emptyEndpoint = new IPEndPoint(IPAddress.Any, 0);
        var buffer = new byte[65535];
        var memory = buffer.AsMemory();
        while (!cancellationToken.IsCancellationRequested)
        {
            var result =
                await Client.Client.ReceiveFromAsync(memory, SocketFlags.None, emptyEndpoint, cancellationToken);

            var length = result.ReceivedBytes;

            if (length < 31)
            {
                continue;
            }

            var header = PackageHeader.ReadData(memory);
            var body = PackageHelper.BodyReaders[header.Type](memory[header.Length..length]);
            var package = new ProtocolPackage(header, body);

            _ = ReadPackageAsync(package, cancellationToken);
        }
    }

    private async Task ReadPackageAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        await _context.HandlePackageAsync(package, cancellationToken);
    }

    public override async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var header = PackageHeader.CreateFromContext(_context, PackageType.DataPackage);
        var dataPackage = new DataPackageBody(buffer);
        var protocolPackage = new ProtocolPackage(header, dataPackage);
        await SendAsync(protocolPackage, cancellationToken);
    }

    public override void Send(ReadOnlyMemory<byte> buffer)
    {
        var header = PackageHeader.CreateFromContext(_context, PackageType.DataPackage);
        var dataPackage = new DataPackageBody(buffer);
        var protocolPackage = new ProtocolPackage(header, dataPackage);
        Send(protocolPackage);
    }

    internal override void Send(ProtocolPackage package)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(package.Length);
        var span = buffer.AsSpan()[..package.Length];
        package.Header.WriteData(span);
        package.Body.WriteData(span[package.Header.Length..]);
        Client.Send(span);
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
        await Client.SendAsync(memory, cancellationToken);
        ArrayPool<byte>.Shared.Return(buffer);
        _context.Tracker.LastSentPackage = package;
    }

    public override Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override byte[] Receive()
    {
        throw new NotImplementedException();
    }

    public override IPEndPoint EndPoint { get; }

    public void Dispose()
    {
        Client.Dispose();
    }
}