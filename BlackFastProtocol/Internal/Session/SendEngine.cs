using System.Threading.Channels;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Ack;
using BlackFastProtocol.Public;

namespace BlackFastProtocol.Internal.Session;

/// <summary>
/// Sliding-window reliable sender.
///
/// TWO TRANSMISSION MODES:
///
///   EnqueueAsync  — single packet (small payload, SendReliableAsync).
///     • Returns as soon as the command is posted to the mailbox — fire and
///       move on. The packet will be sent by PumpAsync as soon as a cwnd slot
///       opens, and retransmitted by TickInternalAsync if the RTO expires.
///     • Does NOT block waiting for the packet to be physically sent.
///       Rationale: the caller (SendReliableAsync) only needs to know the
///       packet was accepted; delivery is guaranteed by RTO retransmit + ACK.
///
///   EnqueueWindowAsync — DataHeader + fragment burst.
///     • ALL packets sent immediately as one burst (no cwnd gating within the
///       burst). Returns only after the receiver ACKs the complete window.
///     • cwnd governs how many concurrent windows are in-flight, not how many
///       packets are inside one window. Gating individual fragments against
///       cwnd causes a deadlock: the last fragment can never be sent because
///       the window is "full", but the ACK that would open the window requires
///       the last fragment to be received first.
///
/// THREAD-SAFETY: mailbox/actor — all state mutations serialised through a
/// Channel. RunAsync is the single consumer.
/// </summary>
internal sealed class SendEngine : IAsyncDisposable
{
    private readonly Queue<InFlightPacket>            _pending  = new();
    private readonly Dictionary<uint, InFlightPacket> _inFlight = new();
    private readonly CongestionController             _cc       = new();
    private readonly FlowController                   _fc;
    private readonly RtoCalculator                    _rto      = new();
    private readonly Func<ProtocolPackage, CancellationToken, ValueTask> _send;

    private readonly Channel<Func<CancellationToken, Task>> _mailbox =
        Channel.CreateUnbounded<Func<CancellationToken, Task>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public SendEngine(Func<ProtocolPackage, CancellationToken, ValueTask> send, int initialReceiverWindow)
    {
        _send = send;
        _fc   = new FlowController(initialReceiverWindow);
    }

    // ── Consumer loop ─────────────────────────────────────────────────────────────

    public async Task RunAsync(CancellationToken ct)
    {
        await foreach (var cmd in _mailbox.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            try   { await cmd(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch { /* errors surface via individual TCS */ }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enqueue a single packet. Returns immediately after the command is posted —
    /// does NOT wait for the packet to be physically sent or ACK-ed.
    /// Delivery is guaranteed by RTO retransmission inside TickInternalAsync.
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
                // Resolve as soon as the command has executed — the packet is
                // either already sent (PumpAsync moved it to _inFlight) or
                // waiting in _pending for a slot to open.
                tcs.TrySetResult();
            }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });

        return tcs.Task.WaitAsync(ct);
    }

    /// <summary>
    /// Send an entire window burst (DataHeader + fragments) atomically.
    /// All packets are sent immediately — no cwnd gating within the burst.
    /// Returns only after the receiver ACKs the full window (last fragment
    /// confirmed in ProcessAckAsync).
    /// </summary>
    public Task EnqueueWindowAsync(IReadOnlyList<ProtocolPackage> window, CancellationToken ct)
    {
        if (window.Count == 0) return Task.CompletedTask;

        var packets    = window.Select(pkg => new InFlightPacket(pkg)).ToArray();
        var windowTcs  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var windowSequences = packets.Select(p => p.Package.Header.Sequence).ToHashSet();

        foreach (var p in packets)
            p.TrackWindow(windowSequences, windowTcs);

        _mailbox.Writer.TryWrite(async innerCt =>
        {
            try
            {
                foreach (var p in packets)
                {
                    p.MarkSent();
                    _inFlight[p.Package.Header.Sequence] = p;
                    await _send(p.Package, innerCt).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                foreach (var p in packets) p.SendTcs.TrySetException(ex);
                windowTcs.TrySetException(ex);
            }
        });

        return windowTcs.Task.WaitAsync(ct);
    }

    /// <summary>Process an incoming ACK.</summary>
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

    /// <summary>Periodic RTO retransmit check. Fire-and-forget.</summary>
    public void PostTick()
    {
        _mailbox.Writer.TryWrite(async innerCt =>
            await TickInternalAsync(innerCt).ConfigureAwait(false));
    }

    /// <summary>Feed a Pong RTT sample. Fire-and-forget.</summary>
    public void UpdateRtt(TimeSpan rtt)
    {
        _mailbox.Writer.TryWrite(_ => { _rto.Update(rtt); return Task.CompletedTask; });
    }

    // ── Private ───────────────────────────────────────────────────────────────────

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

    private async Task ProcessAckAsync(AckBody ack, CancellationToken ct)
    {
        _fc.Update(ack.ReceiverWindow);

        var now      = DateTime.UtcNow;
        var toRemove = new List<uint>();

        foreach (var (seq, p) in _inFlight)
        {
            bool confirmed;
            if (SequenceHelper.GreaterOrEqual(ack.BaseSequence, seq)
                && SequenceHelper.Distance(seq, ack.BaseSequence) <= BlackFastClient.WindowSize)
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
        {
            if (_inFlight.TryGetValue(r, out var p))
            {
                _inFlight.Remove(r);
                // Signal EnqueueWindowAsync that this packet's window is ACK-ed.
                p.SendTcs.TrySetResult();

                if (p.WindowSequences is not null && p.WindowTcs is not null
                                                  && p.WindowSequences.All(seq => !_inFlight.ContainsKey(seq)))
                    p.WindowTcs.TrySetResult();
            }
        }

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

    private async Task RetransmitMissingAsync(AckBody ack, CancellationToken ct)
    {
        var keys = _inFlight.Keys.ToArray();

        foreach (var seq in keys)
        {
            if (!_inFlight.TryGetValue(seq, out var p)) continue;
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

    public ValueTask DisposeAsync()
    {
        _mailbox.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}

// ── Supporting types ──────────────────────────────────────────────────────────────

internal sealed class InFlightPacket
{
    public ProtocolPackage Package { get; }
    public DateTime        SentAt  { get; private set; }
    public int             Retries { get; private set; }

    /// <summary>
    /// Used exclusively by EnqueueWindowAsync: set in ProcessAckAsync when
    /// this packet's sequence is confirmed by the receiver's window ACK.
    /// EnqueueAsync packets do not use this TCS.
    /// </summary>
    public TaskCompletionSource SendTcs { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IReadOnlySet<uint>? WindowSequences { get; private set; }
    public TaskCompletionSource? WindowTcs { get; private set; }

    public InFlightPacket(ProtocolPackage package) => Package = package;

    public void TrackWindow(IReadOnlySet<uint> windowSequences, TaskCompletionSource windowTcs)
    {
        WindowSequences = windowSequences;
        WindowTcs = windowTcs;
    }

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
        if (_cwnd < _ssthresh) _cwnd += 1;
        else                   _cwnd += 1.0 / _cwnd;
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

internal sealed class RtoCalculator
{
    private double _srtt = -1;
    private double _rttvar;
    private double _rto = 200;

    public TimeSpan Rto => TimeSpan.FromMilliseconds(_rto);

    public void Update(TimeSpan rtt)
    {
        var r = rtt.TotalMilliseconds;
        if (_srtt < 0) { _srtt = r; _rttvar = r / 2; }
        else
        {
            _rttvar = 0.75 * _rttvar + 0.25 * Math.Abs(_srtt - r);
            _srtt   = 0.875 * _srtt  + 0.125 * r;
        }
        _rto = Math.Max(_srtt + 4 * _rttvar, 200);
    }
}

internal static class SequenceHelper
{
    public static bool GreaterOrEqual(uint a, uint b) => (int)(a - b) >= 0;
    public static uint Distance(uint from, uint to)   => (uint)(to - from);
}
