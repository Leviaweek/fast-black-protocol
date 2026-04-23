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
            // Single-packet reliable send — write directly and ACK immediately.
            await context.DataChannel.Writer.WriteAsync(package.Data.ToArray(), cancellationToken);

            var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Ack);
            var ack = new AckBody(header.Sequence, 0, FastBlackSessionContext.ComputeReceiverWindow());
            var ackPackage = new ProtocolPackage(responseHeader, ack);

            // BUG-3 FIX: record the outgoing sequence of this ACK so that
            // HandlePackageAsync can find it in SentBuffer when re-sending
            // on duplicate incoming packet detection.
            context.Tracker.LastSentAckOutgoingSequence = responseHeader.Sequence;

            await context.Session.SendAsync(ackPackage, cancellationToken);
        }
        else
        {
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

            var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Ack);
            var ack = new AckBody(header.Sequence, 0, FastBlackSessionContext.ComputeReceiverWindow());
            var ackPackage = new ProtocolPackage(responseHeader, ack);

            // BUG-3 FIX: same tracking for the synchronous path.
            context.Tracker.LastSentAckOutgoingSequence = responseHeader.Sequence;

            context.Session.Send(ackPackage);
        }
        else
        {
            streamClientState.DataAccumulator.TryAdd(header.Sequence, package.Data.Span);
        }

        return true;
    }
}
