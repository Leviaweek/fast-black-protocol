using BlackFastProtocol.Internal.Buffer;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Ack;
using BlackFastProtocol.Internal.State;
using BlackFastProtocol.Public;

namespace BlackFastProtocol.Internal.Session;

internal sealed class FastBlackSessionContext : IAsyncDisposable, IDisposable
{
    // ── Properties ────────────────────────────────────────────────────────────────

    public  BlackFastClient        Session         { get; }
    internal PackageTracker        Tracker         { get; } = new();
    internal SessionInfo           Info            { get; }
    internal SessionDataPipeline   DataChannel     { get; } = new();
    internal SequenceManager       SequenceManager { get; } = new();
    internal ReorderingBuffer      ReorderingBuffer{ get; } = new();
    internal PingPongManager       PingPongManager { get; } = new();
    internal RetransmissionTimeout Rtt             { get; } = new();
    internal SendEngine            SendEngine      { get; }

    // ── Constructor ───────────────────────────────────────────────────────────────

    internal FastBlackSessionContext(BlackFastClient client, Guid sessionId)
    {
        Session = client;
        Info    = new SessionInfo(sessionId);

        SendEngine = new SendEngine(
            (pkg, ct) => client.SendAsync(pkg, ct),
            BlackFastClient.WindowSize);

        _engineCts = new CancellationTokenSource();
        _ = SendEngine.RunAsync(_engineCts.Token);
    }

    // ── Client state (volatile + Interlocked for lock-free cross-thread visibility) ─

    private volatile ClientState _clientState = new DefaultClientState();

    internal ClientState ClientState
    {
        get => _clientState;
        set => Interlocked.Exchange(ref _clientState, value);
    }

    // ── Cancellation ──────────────────────────────────────────────────────────────

    // Dedicated CTS for the engine loop — lives as long as the context.
    private readonly CancellationTokenSource _engineCts;
    // Linked CTS for the retransmit timer and keepalive — cancelled in Dispose.
    private CancellationTokenSource? _tickCts;

    // ── Lifecycle ─────────────────────────────────────────────────────────────────

    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        Info.IsStarted = true;

        // Drain packets that arrived before StartAsync (buffered in ReorderingBuffer).
        while (ReorderingBuffer.TryGetOrderedPackage(SequenceManager.Expected, out var pkg))
            await HandleAllPackageAsync(pkg!, cancellationToken);

        // Start background helpers.  RunAsync is already running (started in ctor).
        _tickCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = RetransmitTickAsync(_tickCts.Token);
        _ = PingPongManager.StartAsync(this, _tickCts.Token);
    }

    private async Task RetransmitTickAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(50, ct);
                SendEngine.PostTick();
            }
            catch (OperationCanceledException) { break; }
        }
    }

    // ── Receive pipeline ──────────────────────────────────────────────────────────

    public async Task HandlePackageAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        var seq      = package.Header.Sequence;
        var expected = SequenceManager.Expected;

        if (!SequenceHelper.GreaterOrEqual(seq, expected))
        {
            // BUG-3 FIX: re-send last ACK on duplicate, using outgoing sequence.
            if (seq == expected - 1 && Tracker.LastSentAckOutgoingSequence.HasValue)
            {
                var lastAck = Tracker.SentBuffer.Peek(Tracker.LastSentAckOutgoingSequence.Value);
                if (lastAck?.Header.Type == PackageType.Ack)
                    await Session.SendAsync(lastAck, cancellationToken);
            }
            return;
        }

        var distance = SequenceHelper.Distance(expected, seq);
        if (distance >= (uint)ReorderingBuffer.Length) return;

        if (!Info.IsStarted)
        {
            ReorderingBuffer.TryAdd(package);
            return;
        }

        if (seq != expected)
        {
            if (ReorderingBuffer.TryAdd(package))
            {
                var receivedMask = ReorderingBuffer.GetPackagesMask(
                    expected, expected + BlackFastClient.WindowSize);
                await SendAckAsync(expected - 1, receivedMask, cancellationToken);
            }
            return;
        }

        await HandleAllPackageAsync(package, cancellationToken);
    }

    private async Task HandleAllPackageAsync(ProtocolPackage package, CancellationToken cancellationToken)
    {
        await _clientState.HandleAsync(package, this, cancellationToken);

        if (Info.IsAborted) { await DisposeAsync(); return; }

        while (ReorderingBuffer.TryGetOrderedPackage(SequenceManager.Expected, out var ordered))
        {
            await _clientState.HandleAsync(ordered!, this, cancellationToken);
            if (Info.IsAborted) { await DisposeAsync(); return; }
        }
    }

    internal static ushort ComputeReceiverWindow()
        => BlackFastClient.WindowSize;

    internal async ValueTask SendAckAsync(uint baseSequence, uint receivedMask,
        CancellationToken cancellationToken)
    {
        var header = PackageHeader.CreateFromContext(this, PackageType.Ack);
        var ack = new AckBody(baseSequence, receivedMask, ComputeReceiverWindow());
        var ackPackage = new ProtocolPackage(header, ack);
        Tracker.LastSentAckOutgoingSequence = header.Sequence;
        await Session.SendAsync(ackPackage, cancellationToken);
    }

    // ── IDisposable / IAsyncDisposable ────────────────────────────────────────────

    private int _disposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (_tickCts is not null)
            await _tickCts.CancelAsync();
        
        _tickCts?.Dispose();
        await SendEngine.DisposeAsync();
        await _engineCts.CancelAsync();
        _engineCts.Dispose();
        DataChannel.Dispose();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _tickCts?.Cancel();
        _tickCts?.Dispose();
        SendEngine.DisposeAsync().AsTask().ContinueWith(_ => { }, TaskScheduler.Default);
        _engineCts.Cancel();
        _engineCts.Dispose();
        DataChannel.Dispose();
    }
}
