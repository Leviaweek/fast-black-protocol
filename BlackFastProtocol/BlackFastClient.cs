using System.Net;
using System.Net.Sockets;

namespace BlackFastProtocol;

public abstract class BlackFastClient(UdpClient client)
{
    public abstract ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
    public abstract void Send(ReadOnlyMemory<byte> buffer);
    public abstract IPEndPoint EndPoint { get; }
    private protected readonly UdpClient Client = client;
}