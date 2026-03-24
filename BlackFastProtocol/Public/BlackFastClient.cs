using System.Buffers;
using System.Net;
using System.Net.Sockets;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Ack;
using BlackFastProtocol.Internal.Package.DataHeader;
using BlackFastProtocol.Internal.Package.DataPackage;
using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Public;

public abstract class BlackFastClient(UdpClient client)
{
    public abstract IPEndPoint EndPoint { get; }
    private protected readonly UdpClient Client = client;
    internal const int MaxPackageSize = 1400;
    internal const int MaxPayloadSize = MaxPackageSize - PackageHeader.Size;
    internal const int WindowSize = 32;
    internal const int MaxWindowPayload = MaxPayloadSize * WindowSize;
    private protected abstract FastBlackSessionContext Context { get; }
    
    protected abstract void SendBytes(ReadOnlySpan<byte> buffer);
    protected abstract ValueTask SendBytesAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct);

    
    public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
    {
        if (buffer.Length > MaxWindowPayload)
            throw new ArgumentOutOfRangeException(nameof(buffer),
                $"Buffer exceeds {MaxWindowPayload} bytes. Use SendAsync(IAsyncEnumerable<...>) for large data.");

        if (buffer.Length <= MaxPayloadSize)
            return SendAsync(GetDataBodyProtocolPackage(buffer), ct);

        return SendFragmentedAsync(buffer, ct);
    }
    
    
    private async ValueTask SendFragmentedAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
    {
        await SendAsync(CreateDataHeaderPackage(buffer.Length), ct);
        var offset = 0;
        while (offset < buffer.Length)
        {
            var size = Math.Min(MaxPayloadSize, buffer.Length - offset);
            await SendAsync(GetDataBodyProtocolPackage(buffer.Slice(offset, size)), ct);
            offset += size;
        }
        await WaitForAckAsync(ct);
    }

    private ProtocolPackage CreateDataHeaderPackage(int totalSize)
    {
        var header = PackageHeader.CreateFromContext(Context, PackageType.DataHeader);
        return new ProtocolPackage(header, new DataHeaderBody(totalSize));
    }
    
    private ProtocolPackage GetDataBodyProtocolPackage(ReadOnlyMemory<byte> buffer)
    {
        var header = PackageHeader.CreateFromContext(Context, PackageType.Data);
        var body = new DataBody(buffer);
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
    
    public async ValueTask SendAsync(IAsyncEnumerable<ReadOnlyMemory<byte>> chunks,
        int totalSize,
        CancellationToken ct = default)
    {
        var dataHeader = CreateDataHeaderPackage(totalSize);
        await SendAsync(dataHeader, ct);

        var sentBytes = 0;
        var sentInWindow = 0;
        
        await foreach (var chunk in chunks.WithCancellation(ct))
        {
            var dataBodyHeader = GetDataBodyProtocolPackage(chunk);
            await SendAsync(dataBodyHeader, ct);
            sentInWindow++;
            sentBytes += chunk.Length;

            if (sentInWindow != WindowSize) continue;
            
            sentInWindow = 0;
            await WaitForAckAsync(ct);
        }

        if (sentInWindow > 0)
            await WaitForAckAsync(ct);

        if (sentBytes != totalSize)
            throw new InvalidOperationException(
                $"Sent {sentBytes} bytes but declared {totalSize} in DataHeader.");
    }

    private async ValueTask<IPackageBody> WaitForAckAsync(CancellationToken ct)
    {
        var tsc = new TaskCompletionSource<IPackageBody>();
        Context.AckAwaiter = tsc;
        var result = await tsc.Task.WaitAsync(ct);
        return result;
    }
    

    public Task<byte[]> ReceiveAsync(CancellationToken ct) =>
        Context.DataChannel.Reader.ReadAsync(ct).AsTask();

    public byte[] Receive(CancellationToken ct) =>
        Context.DataChannel.Reader.ReadAsync(ct).AsTask().GetAwaiter().GetResult();
}