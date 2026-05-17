using System.Buffers;
using System.Net;
using System.Net.Sockets;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.DataHeader;
using BlackFastProtocol.Internal.Package.DataPackage;
using BlackFastProtocol.Internal.Package.Ping;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Public;

public abstract class BlackFastClient(UdpClient client)
{
    public abstract IPEndPoint EndPoint { get; }
    private protected readonly UdpClient Client = client;

    internal const int MaxPackageSize   = 1400;
    internal const int MaxPayloadSize   = MaxPackageSize - PackageHeader.Size;
    internal const int WindowSize       = 32;
    internal const int MaxWindowPayload = MaxPayloadSize * WindowSize;

    private protected abstract FastBlackSessionContext Context { get; }

    protected abstract void SendBytes(ReadOnlySpan<byte> buffer);
    protected abstract ValueTask SendBytesAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct);

    // ── Public send API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Send up to MaxWindowPayload bytes reliably.
    /// Internally fragments if > MaxPayloadSize.
    /// Larger streams: use SendAsync(IAsyncEnumerable).
    /// </summary>
    public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
    {
        if (buffer.Length > MaxWindowPayload)
            throw new ArgumentOutOfRangeException(nameof(buffer),
                $"Buffer exceeds {MaxWindowPayload} bytes. Use SendAsync(IAsyncEnumerable<...>) for large data.");

        if (buffer.Length <= MaxPayloadSize)
            return SendViaEngineAsync(GetDataBodyPackage(buffer), ct);

        return SendFragmentedAsync(buffer, ct);
    }

    /// <summary>Fire-and-forget single packet (no ACK guarantee).</summary>
    public void Send(ReadOnlyMemory<byte> buffer)
        => Send(GetDataBodyPackage(buffer));

    /// <summary>
    /// Send a single small packet with delivery guarantee
    /// (retransmission until ACK or session abort).
    /// </summary>
    public ValueTask SendReliableAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
    {
        if (buffer.Length > MaxPayloadSize)
            throw new ArgumentOutOfRangeException(nameof(buffer),
                $"Buffer exceeds {MaxPayloadSize} bytes. Use SendAsync for larger data.");

        return SendViaEngineAsync(GetDataBodyPackage(buffer), ct);
    }

    /// <summary>
    /// Stream large data window-by-window.
    /// totalSize must exactly equal the sum of all chunk lengths.
    /// </summary>
    public async ValueTask SendAsync(IAsyncEnumerable<ReadOnlyMemory<byte>> chunks,
        int totalSize, CancellationToken ct = default)
    {
        if (totalSize < 0)
            throw new ArgumentOutOfRangeException(nameof(totalSize), "Total size cannot be negative.");

        if (totalSize == 0)
        {
            await SendReliableAsync(ReadOnlyMemory<byte>.Empty, ct);
            return;
        }

        var window   = new List<ProtocolPackage>();
        var sentBytes = 0L;
        var dataPacketsInWindow = 0;

        window.Add(CreateDataHeaderPackage(totalSize));

        async ValueTask AddFragmentAsync(ReadOnlyMemory<byte> fragment)
        {
            window.Add(GetDataBodyPackage(fragment));
            dataPacketsInWindow++;

            if (dataPacketsInWindow == WindowSize)
            {
                await Context.SendEngine.EnqueueWindowAsync(window, ct);
                window.Clear();
                dataPacketsInWindow = 0;
            }
        }

        var fragmentBuffer = new byte[MaxPayloadSize];
        var fragmentBytes = 0;

        await foreach (var chunk in chunks.WithCancellation(ct))
        {
            sentBytes += chunk.Length;
            if (sentBytes > totalSize)
                throw new InvalidOperationException(
                    $"Input stream exceeded declared {totalSize} bytes.");

            var remainingChunk = chunk;
            while (!remainingChunk.IsEmpty)
            {
                var copySize = Math.Min(MaxPayloadSize - fragmentBytes, remainingChunk.Length);
                remainingChunk[..copySize].CopyTo(fragmentBuffer.AsMemory(fragmentBytes));
                fragmentBytes += copySize;
                remainingChunk = remainingChunk[copySize..];

                if (fragmentBytes != MaxPayloadSize) continue;

                await AddFragmentAsync(fragmentBuffer.ToArray());
                fragmentBytes = 0;
            }
        }

        if (fragmentBytes > 0)
            await AddFragmentAsync(fragmentBuffer.AsMemory(0, fragmentBytes).ToArray());

        if (sentBytes != totalSize)
            throw new InvalidOperationException(
                $"Sent {sentBytes} bytes but declared {totalSize} in DataHeader.");

        if (window.Count > 0)
            await Context.SendEngine.EnqueueWindowAsync(window, ct);
    }

    // ── Receive API ───────────────────────────────────────────────────────────────

    public Task<byte[]> ReceiveAsync(CancellationToken ct)
        => Context.DataChannel.Reader.ReadAsync(ct).AsTask();

    public byte[] Receive(CancellationToken ct)
        => Context.DataChannel.Reader.ReadAsync(ct).AsTask().GetAwaiter().GetResult();

    // ── Internal send plumbing ────────────────────────────────────────────────────

    /// <summary>
    /// All sends that require reliable delivery go through the SendEngine.
    /// The engine queues the packet and sends it when the congestion/flow window allows.
    /// </summary>
    private ValueTask SendViaEngineAsync(ProtocolPackage package, CancellationToken ct)
        => new(Context.SendEngine.EnqueueAsync(package, ct));

    private async ValueTask SendFragmentedAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
    {
        // Build the complete window: DataHeader + all fragment packets.
        // They are sent as one atomic burst via EnqueueWindowAsync — no cwnd
        // gating between individual fragments.
        // The method returns only after the receiver ACKs the full window.
        var window = new List<ProtocolPackage>();
        window.Add(CreateDataHeaderPackage(buffer.Length));

        var offset = 0;
        while (offset < buffer.Length)
        {
            var size = Math.Min(MaxPayloadSize, buffer.Length - offset);
            window.Add(GetDataBodyPackage(buffer.Slice(offset, size)));
            offset += size;
        }

        await Context.SendEngine.EnqueueWindowAsync(window, ct);
    }

    private ProtocolPackage CreateDataHeaderPackage(int totalSize)
    {
        var header = PackageHeader.CreateFromContext(Context, PackageType.DataHeader);
        return new ProtocolPackage(header, new DataHeaderBody(totalSize));
    }

    private ProtocolPackage GetDataBodyPackage(ReadOnlyMemory<byte> buffer)
    {
        var header = PackageHeader.CreateFromContext(Context, PackageType.Data);
        return new ProtocolPackage(header, new DataBody(buffer));
    }

    // ── Low-level wire send (used by SendEngine callback + direct ACK/Ping) ───────

    internal async ValueTask SendAsync(ProtocolPackage package, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(package.Length);
        try
        {
            var memory = buffer.AsMemory()[..package.Length];
            package.Header.WriteData(memory.Span);
            package.Body.WriteData(memory.Span[package.Header.Length..]);
            await SendBytesAsync(memory, ct);
            Context.Tracker.SentBuffer.Set(package);
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
            Context.Tracker.SentBuffer.Set(package);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

// ── RTT tracker (kept for Ping/Pong RTT measurement) ─────────────────────────────

internal sealed class RetransmissionTimeout
{
    private const double Alpha = 0.125;
    private const double Beta  = 0.25;
    private bool   _isFirst = true;
    private double _srtt;
    private double _rttvar;

    public double Timeout
    {
        get => Volatile.Read(ref field);
        private set => Volatile.Write(ref field, value);
    }

    public void UpdateRtt(TimeSpan measuredRtt)
    {
        var ms = measuredRtt.TotalMilliseconds;
        if (_isFirst)
        {
            _srtt   = ms;
            _rttvar = ms / 2.0;
            _isFirst = false;
        }
        else
        {
            var err = Math.Abs(ms - _srtt);
            _rttvar = (1 - Beta)  * _rttvar + Beta  * err;
            _srtt   = (1 - Alpha) * _srtt   + Alpha * ms;
        }
        Timeout = Math.Max(_srtt + 4 * _rttvar, 200);
    }

    private const double MinTimeout = 200;
}

// ── Ping/Pong keepalive ───────────────────────────────────────────────────────────

internal sealed class PingPongManager
{
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DeadTimeout  = TimeSpan.FromSeconds(15);

    public async Task StartAsync(FastBlackSessionContext context, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Use LOCAL receive time to avoid clock-skew issues (fix #8).
            var lastReceived = context.Tracker.LastReceivedLocalTime;
            var idle = lastReceived is null
                ? TimeSpan.MinValue
                : DateTimeOffset.UtcNow - lastReceived.Value;

            if (idle >= DeadTimeout)
            {
                context.Info.IsAborted = true;
                context.Dispose();
                return;
            }

            if (idle >= PingInterval)
            {
                var header = PackageHeader.CreateFromContext(context, PackageType.Ping);
                context.Tracker.PendingPingSequence = header.Sequence;
                context.Tracker.PingSentTimestamp   = DateTimeOffset.UtcNow;
                await context.Session.SendAsync(new ProtocolPackage(header, new PingBody()), ct);
                await Task.Delay(PingInterval, ct);
            }
            else
            {
                await Task.Delay(PingInterval - idle, ct);
            }
        }
    }
}
