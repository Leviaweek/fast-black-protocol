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
        Context  = new FastBlackSessionContext(this, Guid.CreateVersion7());
        // NOTE: FastBlackSessionContext constructor already starts SendEngine.RunAsync,
        // so EnqueueAsync is safe to call as soon as this object is constructed.
    }

    public async Task ConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        Client.Connect(endPoint);

        // Start background tasks first so the receive loop and session are up
        // before the Handshake packet is sent (and before any ACK can arrive).
        _ = ReceiveLoop(cancellationToken);
        _ = Context.StartAsync(cancellationToken);

        // Send Handshake via the synchronous low-level wire path (bypasses the
        // SendEngine mailbox entirely).  Handshake is fire-and-forget by design —
        // it does not need delivery guarantees, and sending it through the engine
        // would be redundant because the engine is already running.
        var handshakeHeader = PackageHeader.CreateFromContext(Context, PackageType.Handshake);
        Send(new ProtocolPackage(handshakeHeader, new HandshakeBody()));

        // Give the listener a moment to process the handshake and register the
        // session before the caller starts sending data.  This is needed because
        // StartAsync on the server side is called from AcceptClientAsync, which
        // the test must await before calling ReceiveAsync.
        await Task.Delay(50, cancellationToken);
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var emptyEndpoint = new IPEndPoint(IPAddress.Any, 0);
        var buffer        = new byte[MaxPackageSize];
        var memory        = buffer.AsMemory();

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await Client.Client.ReceiveFromAsync(
                memory, SocketFlags.None, emptyEndpoint, cancellationToken);

            var length = result.ReceivedBytes;
            if (length < PackageHeader.Size) continue;

            var header = PackageHeader.ReadData(memory);

            if (!PackageHelper.BodyReaders.TryGetValue(header.Type, out var bodyReader))
                continue;

            // Copy body bytes before the next receive overwrites the shared buffer.
            var bodyBytes = memory[header.Length..length].ToArray().AsMemory();
            var body      = bodyReader(bodyBytes);
            var package   = new ProtocolPackage(header, body);

            await Context.HandlePackageAsync(package, cancellationToken);
        }
    }

    protected override void SendBytes(ReadOnlySpan<byte> buffer)
        => Client.Send(buffer);

    protected override async ValueTask SendBytesAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
        => await Client.SendAsync(buffer, ct);

    public override IPEndPoint EndPoint { get; }

    public void Dispose() => Client.Dispose();
}
