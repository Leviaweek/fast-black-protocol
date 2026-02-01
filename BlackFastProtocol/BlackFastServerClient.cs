using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace BlackFastProtocol;

public sealed class BlackFastServerClient(
    UdpClient client,
    IPEndPoint remoteEndPoint,
    Channel<UdpPackage> dataChanel,
    Action dispose)
    : BlackFastClient(client), IDisposable
{
    private volatile IPEndPoint _remoteEndPoint = remoteEndPoint;
    internal readonly Channel<UdpPackage> DataChanel = dataChanel;

    internal void UpdateEndpoint(IPEndPoint remoteEndPoint)
    {
        Interlocked.Exchange(ref _remoteEndPoint, remoteEndPoint);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        dispose();
    }

    ~BlackFastServerClient()
    {
        Dispose();
    }
}