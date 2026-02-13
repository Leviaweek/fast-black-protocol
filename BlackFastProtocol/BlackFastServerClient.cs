using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using UdpServer.Packages;
using UdpServer.Packages.Handshake;

namespace BlackFastProtocol;

public sealed class BlackFastServerClient(
    UdpClient client,
    IPEndPoint remoteEndPoint,
    Channel<UdpPackage> dataChannel,
    Action dispose)
    : BlackFastClient(client), IDisposable
{
    private readonly Dictionary<PackageType, IPackageHandler> _handlers = new() {
        [PackageType.Handshake] = new PackageHandlerAdapter<HandshakePackage>(new HandshakePackageHandler()),
        
    };

    private volatile IPEndPoint _remoteEndPoint = remoteEndPoint;
    internal readonly Channel<UdpPackage> DataChannel = dataChannel;

    internal void UpdateEndpoint(IPEndPoint remoteEndPoint)
    {
        Interlocked.Exchange(ref _remoteEndPoint, remoteEndPoint);
    }

    private async Task ReadPackagesAsync(CancellationToken cancellationToken)
    {
        var context = new FastBlackSessionConext();
        await foreach (var udpPackage in DataChannel.Reader.ReadAllAsync(cancellationToken))
        {
            using (udpPackage)
            {
                var span = udpPackage.Data.Span;
                var packageType = (PackageType)span[0];

                await _handlers[packageType].HandlePackageAsync(udpPackage.Data, context, cancellationToken);

                if (context.IsAborted)
                {
                    Dispose();
                    break;
                }
            }

        }
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