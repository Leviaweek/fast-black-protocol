using BlackFastProtocol.Internal.Package.Ack;
using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;
using BlackFastProtocol.Internal.State;

namespace BlackFastProtocol.Internal.Package.DataPackage;

internal sealed class DataBodyHandler : IBodyHandler<DataBody>
{
    public async ValueTask<bool> TryHandlePackageAsync(PackageHeader header, DataBody package,
        FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        if (context.ClientState is not StreamClientState streamClientState)
            return false;

        if (streamClientState.DataAccumulator is null)
        {
            // ── Single-packet path ─────────────────────────────────────────────
            // No DataHeader was sent → this is a standalone reliable packet.
            // Write to channel and ACK immediately.
            await context.DataChannel.Writer.WriteAsync(package.Data.ToArray(), cancellationToken);

            await context.SendAckAsync(header.Sequence, 0, cancellationToken);
        }
        else
        {
            // ── Fragmented path ────────────────────────────────────────────────
            // Add fragment to the accumulator.
            // NO per-fragment ACK — the receiver sends ONE ACK for the complete
            // window in StreamClientState.PostProcessDataAsync (when IsReady()).
            // The sender uses EnqueueWindowAsync which sends all fragments as a
            // single burst and waits for the single window-level ACK.
            streamClientState.DataAccumulator.TryAdd(header.Sequence, package.Data.Span);
        }

        return true;
    }

    public bool TryHandlePackage(PackageHeader header, DataBody package, FastBlackSessionContext context)
    {
        if (context.ClientState is not StreamClientState streamClientState)
            return false;

        if (streamClientState.DataAccumulator is null)
        {
            context.DataChannel.Writer.TryWrite(package.Data.ToArray());

            _ = context.SendAckAsync(header.Sequence, 0, CancellationToken.None);
        }
        else
        {
            streamClientState.DataAccumulator.TryAdd(header.Sequence, package.Data.Span);
        }

        return true;
    }
}
