using System.Net;
using System.Net.Sockets;
using BlackFastProtocol.Package;

namespace BlackFastProtocol;

public abstract class BlackFastClient(UdpClient client)
{
    public abstract ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
    public abstract void Send(ReadOnlyMemory<byte> buffer);
    internal abstract void Send<TPackage>(TPackage package) where TPackage : PackageBase, IWriteablePackage;
    internal abstract ValueTask SendAsync<TPackage>(TPackage package, CancellationToken cancellationToken) where TPackage : PackageBase, IWriteablePackage;
    public abstract IPEndPoint EndPoint { get; }
    private protected readonly UdpClient Client = client;
}