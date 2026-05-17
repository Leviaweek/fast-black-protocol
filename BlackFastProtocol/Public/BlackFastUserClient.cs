using System.Net;
using System.Net.Sockets;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Handshake;
using BlackFastProtocol.Internal.Session;
using BlackFastProtocol.Internal.State;

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
        
        _ = ReceiveLoop(cancellationToken);
        _ = Context.StartAsync(cancellationToken);

        Context.Info.IsHandshake = true;
        
        var handshakeHeader = PackageHeader.CreateFromContext(Context, PackageType.Handshake);
        await SendAsync(new ProtocolPackage(handshakeHeader, new HandshakeBody()), cancellationToken);

        if (Context.ClientState is not DefaultClientState clientState)
            throw new InvalidOperationException("Unexpected client state after handshake.");
        
        await clientState.Source.Task.WaitAsync(cancellationToken);
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

            if (!PackageHelper.TryReadPackage(memory[..length], out var package))
                continue;

            if (package!.Header.SessionId != Context.Info.SessionId)
                continue;

            await Context.HandlePackageAsync(package, cancellationToken);
        }
    }

    protected override void SendBytes(ReadOnlySpan<byte> buffer)
        => Client.Send(buffer);

    protected override async ValueTask SendBytesAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
        => await Client.SendAsync(buffer, ct);

    public override IPEndPoint EndPoint { get; }

    public void Dispose()
    {
        Context.Dispose();
        Client.Dispose();
    }
}
