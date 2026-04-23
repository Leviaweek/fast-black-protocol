using BlackFastProtocol.Internal.Buffer;
using BlackFastProtocol.Internal.Package;

namespace BlackFastProtocol.Internal.Session;

internal sealed class PackageTracker
{
    public ProtocolPackage? LastReceivedPackage { get; set; }

    /// <summary>
    /// Local wall-clock time at which the last packet was successfully handled.
    /// Used by PingPongManager instead of Header.Timestamp to avoid clock-skew
    /// issues when peers have unsynchronised clocks.
    /// </summary>
    public DateTimeOffset? LastReceivedLocalTime { get; set; }

    public uint? PendingPingSequence { get; set; }
    public DateTimeOffset? PingSentTimestamp { get; set; }

    /// <summary>
    /// BUG-3 FIX: the OUTGOING sequence number of the most recently sent ACK.
    ///
    /// Context: HandlePackageAsync detects duplicate incoming packets (diff &lt; 0)
    /// and wants to re-send the last ACK. It used to look up SentBuffer with
    /// (SequenceManager.Expected - 1), but SentBuffer is keyed by OUTGOING sequence
    /// numbers while Expected is an INCOMING sequence number — completely different
    /// spaces. The lookup would return null or a wrong packet, so the duplicate-ACK
    /// resend was silently a no-op.
    ///
    /// Fix: every place that sends an ACK via context.Session.SendAsync now sets
    /// this field to the outgoing sequence of that ACK packet, giving
    /// HandlePackageAsync a correct key for SentBuffer.Peek().
    /// </summary>
    public uint? LastSentAckOutgoingSequence { get; set; }

    public OutgoingBuffer SentBuffer { get; } = new();
}
