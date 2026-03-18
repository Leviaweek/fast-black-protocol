using System.Net;
using System.Net.Sockets;
using BlackFastProtocol.Internal.Package;

namespace BlackFastProtocol.Public;

public abstract class BlackFastClient(UdpClient client)
{
    public abstract ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
    public abstract void Send(ReadOnlyMemory<byte> buffer);
    internal abstract void Send(ProtocolPackage package);
    internal abstract ValueTask SendAsync(ProtocolPackage package, CancellationToken cancellationToken);
    public abstract Task<byte[]> ReceiveAsync(CancellationToken cancellationToken);
    public abstract byte[] Receive();
    public abstract IPEndPoint EndPoint { get; }
    private protected readonly UdpClient Client = client;
    public const int MaxPackageSize = 1000;
}