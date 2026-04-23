using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Package.Ping;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.Package.Pong;

internal sealed class PongBodyHandler : IBodyHandler<PongBody>
{
    public ValueTask<bool> TryHandlePackageAsync(PackageHeader header, PongBody package,
        FastBlackSessionContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(TryHandlePackage(header, package, context));

    public bool TryHandlePackage(PackageHeader header, PongBody package, FastBlackSessionContext context)
    {
        if (context.Tracker.PendingPingSequence is not { } sequence) return false;

        // Validate that what we sent at that sequence really was a Ping.
        var result = context.Tracker.SentBuffer.Peek(sequence)?.Body is PingBody;
        context.Tracker.PendingPingSequence = null;

        if (result && context.Tracker.PingSentTimestamp is { } sentTimestamp)
        {
            var rtt = DateTimeOffset.UtcNow - sentTimestamp;
            // Feed RTT measurement into the session's retransmission timeout calculator.
            context.Rtt.UpdateRtt(rtt);
            // Also update the SendEngine's own RTO so retransmits stay accurate.
            context.SendEngine.UpdateRtt(rtt);
            context.Tracker.PingSentTimestamp = null;
        }

        return result;
    }
}
