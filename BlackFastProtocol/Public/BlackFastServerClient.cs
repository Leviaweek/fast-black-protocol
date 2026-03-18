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
    private protected override FastBlackSessionContext Context { get; }

    public BlackFastServerClient(UdpClient client,
        IPEndPoint remoteEndPoint,
        Guid sessionId,
        Action dispose) : base(client)
    {
        _dispose = dispose;
        _remoteEndPoint = remoteEndPoint;
        EndPoint = client.Client.LocalEndPoint as IPEndPoint ?? throw new InvalidOperationException("LocalEndPoint is not an IPEndPoint");
        Context = new FastBlackSessionContext(this, sessionId);
    }

    internal Task StartAsync(CancellationToken cancellationToken) => Context.StartAsync(cancellationToken);


    public override IPEndPoint EndPoint { get; }
    
    protected override void SendBytes(ReadOnlySpan<byte> buffer) => Client.Send(buffer, _remoteEndPoint);
    protected override async ValueTask SendBytesAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct) =>
        await Client.SendAsync(buffer, _remoteEndPoint, ct);

    internal void UpdateEndpoint(IPEndPoint remoteEndPoint)
    {
        Interlocked.Exchange(ref _remoteEndPoint, remoteEndPoint);
    }

    internal Task ReadPackageAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        return Context.HandlePackageAsync(package, cancellationToken);
    }
    
    

    public void Dispose()
    {
        _dispose();
        Context.Dispose();
    }
}