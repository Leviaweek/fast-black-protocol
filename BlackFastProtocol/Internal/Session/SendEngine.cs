using System.Threading.Channels;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Ack;

namespace BlackFastProtocol.Internal.Session;

/// <summary>
/// Sliding-window reliable sender with:
///   • SACK-based acknowledgement  (AckBody.BaseSequence + ReceivedMask)
///   • Congestion control          (AIMD slow-start / congestion-avoidance)
///   • Flow control                (receiver-advertised window via AckBody.ReceiverWindow)
///   • Per-packet RTO              (RFC 6298 EWMA, minimum 200 ms)
///   • Mailbox pattern             (all mutations serialised through a Channel — no locks)
///
/// THREAD-SAFETY MODEL (mailbox / actor pattern):
///   Previously _pending, _inFlight, _congestionController, _flowController and _rto were
///   mutated from three concurrent sources:
///     1. ReceiveLoop   → OnAckAsync     (removes confirmed packets, fast-retransmits)
///     2. RetransmitTick → TickAsync     (RTO retransmits, reads _inFlight)
///     3. EnqueueAsync  (caller thread)  (adds to _pending, calls PumpAsync)
///   All three can run truly in parallel on the .NET thread pool, producing data races on
///   Dictionary and Queue which are NOT thread-safe.
///
///   Fix: every public mutating call posts a lambda into an unbounded Channel{Command}.
///   A single background consumer loop (RunAsync) dequeues and executes them one-by-one,
///   so _pending / _inFlight / controllers are always accessed from exactly one logical
///   context at a time — no locks, no ConcurrentDictionary, no torn reads.
///
///   EnqueueAsync / OnAckAsync return a Task that completes only after the command
///   has actually been executed (via TaskCompletionSource), so callers that await them
///   get correct back-pressure and ordering guarantees.
///   TickAsync and UpdateRtt are fire-and-forget posts (no completion signal needed).
/// </summary>
internal sealed class SendEngine : IAsyncDisposable
{
    // ── Internal state — touched ONLY inside the consumer loop ────────────────────

    private readonly Queue<InFlightPacket>             _pending  = new();
    private readonly Dictionary<uint, InFlightPacket>  _inFlight = new();
    private readonly CongestionController              _cc       = new();
    private readonly FlowController                    _fc;
    private readonly RtoCalculator                     _rto      = new();
    private readonly Func<ProtocolPackage, CancellationToken, ValueTask> _send;

    // ── Mailbox ───────────────────────────────────────────────────────────────────

    // Commands are Func<CancellationToken, Task> so they can share the caller's ct.
    private readonly Channel<Func<CancellationToken, Task>> _mailbox =
        Channel.CreateUnbounded<Func<CancellationToken, Task>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public SendEngine(Func<ProtocolPackage, CancellationToken, ValueTask> send, int initialReceiverWindow)
    {
        _send = send;
        _fc   = new FlowController(initialReceiverWindow);
    }

    // ── Consumer loop — call once from FastBlackSessionContext.StartAsync ─────────

    /// <summary>
    /// Runs the mailbox consumer. Must be started exactly once per session and
    /// cancelled via the session's CancellationToken when the session ends.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        await foreach (var cmd in _mailbox.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            try   { await cmd(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch { /* swallow per-command errors — individual callers see them via TCS */ }
        }
    }

    // ── Public API — all post into the mailbox ────────────────────────────────────

    /// <summary>
    /// Enqueue a packet for reliable delivery.
    /// Awaiting this returns after the packet has been handed to the wire (or queued
    /// behind the congestion window), not after it has been ACK-ed.
    /// </summary>
    public Task EnqueueAsync(ProtocolPackage package, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _mailbox.Writer.TryWrite(async innerCt =>
        {
            try
            {
                _pending.Enqueue(new InFlightPacket(package));
                await PumpAsync(innerCt).ConfigureAwait(false);
                tcs.TrySetResult();
            }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });

        return tcs.Task;
    }

    /// <summary>
    /// Process an incoming ACK. Removes confirmed packets, fast-retransmits missing
    /// ones, and pumps pending packets into the now-freed window slots.
    /// </summary>
    public Task OnAckAsync(AckBody ack, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _mailbox.Writer.TryWrite(async innerCt =>
        {
            try
            {
                await ProcessAckAsync(ack, innerCt).ConfigureAwait(false);
                tcs.TrySetResult();
            }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });

        return tcs.Task;
    }

    /// <summary>
    /// Periodic RTO retransmit check. Fire-and-forget — caller does not need to await.
    /// </summary>
    public void PostTick()
    {
        _mailbox.Writer.TryWrite(async innerCt =>
        {
            await TickInternalAsync(innerCt).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Feed a Pong RTT sample into the RTO calculator. Fire-and-forget.
    /// </summary>
    public void UpdateRtt(TimeSpan rtt)
    {
        _mailbox.Writer.TryWrite(_ =>
        {
            _rto.Update(rtt);
            return Task.CompletedTask;
        });
    }

    // ── Private implementation — runs inside consumer loop ────────────────────────

    private async Task ProcessAckAsync(AckBody ack, CancellationToken ct)
    {
        _fc.Update(ack.ReceiverWindow);

        var now      = DateTime.UtcNow;
        var toRemove = new List<uint>();

        foreach (var (seq, p) in _inFlight)
        {
            bool confirmed;

            if (SequenceHelper.GreaterOrEqual(ack.BaseSequence, seq))
            {
                confirmed = true;
            }
            else
            {
                var dist = SequenceHelper.Distance(ack.BaseSequence + 1, seq);
                confirmed = dist < 32 && ((ack.ReceivedMask >> (int)dist) & 1) != 0;
            }

            if (!confirmed) continue;

            _rto.Update(now - p.SentAt);
            _cc.OnAck();
            toRemove.Add(seq);
        }

        foreach (var r in toRemove)
            _inFlight.Remove(r);

        await RetransmitMissingAsync(ack, ct).ConfigureAwait(false);
        await PumpAsync(ct).ConfigureAwait(false);
    }

    private async Task TickInternalAsync(CancellationToken ct)
    {
        var rto  = _rto.Rto;
        var keys = _inFlight.Keys.ToArray();

        foreach (var seq in keys)
        {
            if (!_inFlight.TryGetValue(seq, out var p)) continue;
            if (!p.IsTimeout(rto)) continue;

            _cc.OnLoss();
            p.MarkSent();
            await _send(p.Package, ct).ConfigureAwait(false);
        }
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        while (_pending.Count > 0
               && _inFlight.Count < Math.Min(_cc.Window, _fc.Available))
        {
            var p = _pending.Dequeue();
            p.MarkSent();
            _inFlight[p.Package.Header.Sequence] = p;
            await _send(p.Package, ct).ConfigureAwait(false);
        }
    }

    private async Task RetransmitMissingAsync(AckBody ack, CancellationToken ct)
    {
        var keys = _inFlight.Keys.ToArray();

        foreach (var seq in keys)
        {
            if (!_inFlight.TryGetValue(seq, out var p)) continue;

            // BUG-2 FIX: skip packets already confirmed by cumulative BaseSequence.
            if (SequenceHelper.GreaterOrEqual(ack.BaseSequence, seq)) continue;

            var dist = SequenceHelper.Distance(ack.BaseSequence + 1, seq);
            if (dist >= 32) continue;

            var bit = (ack.ReceivedMask >> (int)dist) & 1;
            if (bit != 0) continue;

            _cc.OnLoss();
            p.MarkSent();
            await _send(p.Package, ct).ConfigureAwait(false);
        }
    }

    // ── IAsyncDisposable ──────────────────────────────────────────────────────────

    public ValueTask DisposeAsync()
    {
        _mailbox.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────────

internal sealed class InFlightPacket
{
    public ProtocolPackage Package { get; }
    public DateTime        SentAt  { get; private set; }
    public int             Retries { get; private set; }

    public InFlightPacket(ProtocolPackage package) => Package = package;

    public void MarkSent()
    {
        SentAt = DateTime.UtcNow;
        Retries++;
    }

    public bool IsTimeout(TimeSpan rto) => DateTime.UtcNow - SentAt > rto;
}

internal sealed class CongestionController
{
    private double _cwnd     = 4;
    private double _ssthresh = 32;

    public int Window => Math.Max(1, (int)_cwnd);

    public void OnAck()
    {
        if (_cwnd < _ssthresh)
            _cwnd += 1;
        else
            _cwnd += 1.0 / _cwnd;
    }

    public void OnLoss()
    {
        _ssthresh = Math.Max(1, _cwnd / 2);
        _cwnd     = _ssthresh;
    }
}

internal sealed class FlowController
{
    private int _window;
    public FlowController(int initial) => _window = initial;
    public void Update(int w)          => _window = Math.Max(0, w);
    public int Available               => _window;
}

/// <summary>RFC 6298 EWMA retransmission timeout calculator.</summary>
internal sealed class RtoCalculator
{
    private double _srtt = -1;
    private double _rttvar;
    private double _rto = 200;

    public TimeSpan Rto => TimeSpan.FromMilliseconds(_rto);

    public void Update(TimeSpan rtt)
    {
        var r = rtt.TotalMilliseconds;

        if (_srtt < 0)
        {
            _srtt   = r;
            _rttvar = r / 2;
        }
        else
        {
            _rttvar = 0.75 * _rttvar + 0.25 * Math.Abs(_srtt - r);
            _srtt   = 0.875 * _srtt  + 0.125 * r;
        }

        _rto = Math.Max(_srtt + 4 * _rttvar, 200);
    }
}

/// <summary>Wrap-safe uint sequence number helpers.</summary>
internal static class SequenceHelper
{
    public static bool GreaterOrEqual(uint a, uint b) => (int)(a - b) >= 0;
    public static uint Distance(uint from, uint to)   => (uint)(to - from);
}
