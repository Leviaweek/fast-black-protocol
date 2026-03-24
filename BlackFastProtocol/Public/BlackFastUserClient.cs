using System.Net;
using System.Net.Sockets;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Handshake;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Public;

public sealed class BlackFastUserClient : BlackFastClient, IDisposable
{

    private protected override FastBlackSessionContext Context { get; }
    
    public BlackFastUserClient(IPEndPoint endPoint) : base(new UdpClient(endPoint))
    {
        EndPoint = endPoint;
        Context = new FastBlackSessionContext(this, Guid.CreateVersion7());
    }

    public async Task ConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        Client.Connect(endPoint);
        var handshakeHeader = PackageHeader.CreateFromContext(Context, PackageType.Handshake);
        var handshakeBody = new HandshakeBody();
        var handshakePackage = new ProtocolPackage(handshakeHeader, handshakeBody);
        await SendAsync(handshakePackage, cancellationToken);
        _ = ReceiveLoop(cancellationToken);
        _ = Context.StartAsync(cancellationToken);
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var emptyEndpoint = new IPEndPoint(IPAddress.Any, 0);
        var buffer = new byte[MaxPackageSize];
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

            await Context.HandlePackageAsync(package, cancellationToken);
        }
    }

    protected override void SendBytes(ReadOnlySpan<byte> buffer) => Client.Send(buffer);

    protected override async ValueTask SendBytesAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct) =>
        await Client.SendAsync(buffer, ct);

    public override IPEndPoint EndPoint { get; }

    public void Dispose()
    {
        Client.Dispose();
    }
}