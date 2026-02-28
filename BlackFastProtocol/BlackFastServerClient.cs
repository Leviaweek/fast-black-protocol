using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using BlackFastProtocol.Package;
using BlackFastProtocol.Package.DataPackage;

namespace BlackFastProtocol;

public sealed class BlackFastServerClient : BlackFastClient, IDisposable
{
    private volatile IPEndPoint _remoteEndPoint;
    private readonly Action _dispose;
    private readonly FastBlackSessionContext _context;
    private readonly ReorderingBuffer _reorderingBuffer;
    private bool _isStarted;
    private volatile uint _expectedSequence = uint.MinValue;
    private readonly ChannelReader<byte[]> _reader;

    public BlackFastServerClient(UdpClient client,
        IPEndPoint remoteEndPoint,
        Guid sessionId,
        Action dispose) : base(client)
    {
        _dispose = dispose;
        _remoteEndPoint = remoteEndPoint;
        EndPoint = client.Client.LocalEndPoint as IPEndPoint ?? throw new InvalidOperationException("LocalEndPoint is not an IPEndPoint");
        var channel = Channel.CreateUnbounded<byte[]>();
        _context = new FastBlackSessionContext(this, sessionId, channel.Writer);
        _reader = channel.Reader;
        _reorderingBuffer = new ReorderingBuffer();
    }

    internal void Start() => _isStarted = true;


    public override IPEndPoint EndPoint { get; }

    internal void UpdateEndpoint(IPEndPoint remoteEndPoint)
    {
        Interlocked.Exchange(ref _remoteEndPoint, remoteEndPoint);
    }

    internal async Task ReadPackageAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        var diff = (int)package.Header.Sequence - _expectedSequence;
        if (diff < 0 || diff >= _reorderingBuffer.Length)
        {
            return;
        }

        if (package.Header.Sequence != _expectedSequence)
        {
            if (!_reorderingBuffer.TryAdd(package))
            {
                throw new ArgumentException("Incorrect argument", nameof(package));
            }

            return;
        }
        
        if (!_isStarted)
        {
            return;
        }

        Interlocked.Increment(ref _expectedSequence);
        await PackageHelper.Handlers[package.Header.Type].HandlePackageAsync(package, _context, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_reorderingBuffer.TryGetOrderedPackage(_expectedSequence, out var orderedPackage))
            {
                return;
            }
            Interlocked.Increment(ref _expectedSequence);
            await PackageHelper.Handlers[orderedPackage!.Header.Type].HandlePackageAsync(orderedPackage, _context, cancellationToken);
        }

        if (!_context.IsAborted) return;
        Dispose();
    }

    public override async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var protocolPackage = GetProtocolPackage(buffer);
        await SendAsync(protocolPackage, cancellationToken);
    }

    public override void Send(ReadOnlyMemory<byte> buffer)
    {
        var protocolPackage = GetProtocolPackage(buffer);
        Send(protocolPackage);
    }

    private ProtocolPackage GetProtocolPackage(ReadOnlyMemory<byte> buffer)
    {
        var nextSequence = _context.GetNextSequence();
        var header = new PackageHeader(_context.SessionId, PackageType.DataPackage, nextSequence);
        var dataPackage = new DataPackageBody(buffer);
        var protocolPackage = new ProtocolPackage(header, dataPackage);
        return protocolPackage;
    }

    internal override void Send(ProtocolPackage package)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(package.Length);
        var span = buffer.AsSpan();
        package.Header.WriteData(span);
        package.Body.WriteData(span[package.Header.Length..]);
        Client.Send(span, _remoteEndPoint);
        ArrayPool<byte>.Shared.Return(buffer);
        _context.LastSentPackage = package;
    }

    internal override async ValueTask SendAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(package.Length);
        var span = buffer.AsSpan();
        package.Header.WriteData(span);
        package.Body.WriteData(span[package.Header.Length..]);
        await Client.SendAsync(buffer, _remoteEndPoint, cancellationToken);
        ArrayPool<byte>.Shared.Return(buffer);
        _context.LastSentPackage = package;
    }

    public byte[] ReceiveAsync(CancellationToken cancellationToken)
    {
        //ToDo
        return [];
    }


    public void Dispose()
    {
        _dispose();
    }
}