using System.Buffers;
using System.Net;
using System.Net.Sockets;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.DataPackage;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Public;

public abstract class BlackFastClient(UdpClient client)
{
    public abstract IPEndPoint EndPoint { get; }
    private protected readonly UdpClient Client = client;
    public const int MaxPackageSize = 1000;
    private protected abstract FastBlackSessionContext Context { get; }
    
    protected abstract void SendBytes(ReadOnlySpan<byte> buffer);
    protected abstract ValueTask SendBytesAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct);

    
    public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
    {
        var package = GetDataBodyProtocolPackage(buffer);
        return SendAsync(package, ct);
    }

    private ProtocolPackage GetDataBodyProtocolPackage(ReadOnlyMemory<byte> buffer)
    {
        var header = PackageHeader.CreateFromContext(Context, PackageType.DataPackage);
        var body = new DataPackageBody(buffer);
        var package = new ProtocolPackage(header, body);
        return package;
    }

    public void Send(ReadOnlyMemory<byte> buffer)
    {
        var package = GetDataBodyProtocolPackage(buffer);
        Send(package);
    }
    
    internal async ValueTask SendAsync(ProtocolPackage package, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(package.Length);
        try
        {
            var memory = buffer.AsMemory()[..package.Length];
            package.Header.WriteData(memory.Span);
            package.Body.WriteData(memory.Span[package.Header.Length..]);
            await SendBytesAsync(memory, ct);
            Context.Tracker.LastSentPackage = package;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    
    internal void Send(ProtocolPackage package)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(package.Length);
        try
        {
            var span = buffer.AsSpan()[..package.Length];
            package.Header.WriteData(span);
            package.Body.WriteData(span[package.Header.Length..]);
            SendBytes(span);
            Context.Tracker.LastSentPackage = package;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public Task<byte[]> ReceiveAsync(CancellationToken ct) =>
        Context.DataChannel.Reader.ReadAsync(ct).AsTask();

    public byte[] Receive(CancellationToken ct) =>
        Context.DataChannel.Reader.ReadAsync(ct).AsTask().GetAwaiter().GetResult();
}