using System.Threading.Channels;
using BlackFastProtocol.Internal.Buffer;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Ack;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.State;

internal sealed class StreamClientState(ChannelWriter<byte[]> dataChannel)
    : ClientState, IDisposable
{
    internal DataAccumulator? DataAccumulator { get; set; } = null;

    public override async ValueTask HandleAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        context.SequenceManager.AdvanceExpected();
        await PackageHelper.Handlers[package.Header.Type].HandlePackageAsync(package, context, cancellationToken);
        await PostProcessDataAsync(context, cancellationToken);
    }

    private async Task PostProcessDataAsync(FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        if (DataAccumulator is null) return;
        if (!DataAccumulator.Window.IsReady()) return;

        // Flush the completed window — DataAccumulator writes directly to DataChannel.
        DataAccumulator.FlushWindow();

        // ACK the last sequence in the completed window.
        // The sender expects ACK(EndSequence - 1) regardless of which packet
        // happened to complete the window (could be out-of-order).
        var lastWindowSequence = DataAccumulator.Window.EndSequence - 1;

        var header = PackageHeader.CreateFromContext(context, PackageType.Ack);
        var ack = new AckBody(lastWindowSequence, 0, FastBlackSessionContext.ComputeReceiverWindow());
        var ackPackage = new ProtocolPackage(header, ack);

        // BUG-3 FIX: record outgoing sequence so HandlePackageAsync can re-send
        // this ACK when a duplicate incoming packet is detected.
        context.Tracker.LastSentAckOutgoingSequence = header.Sequence;

        await context.Session.SendAsync(ackPackage, cancellationToken);

        if (!DataAccumulator.IsComplete())
        {
            DataAccumulator.UpdateWindow();
        }
        else
        {
            DataAccumulator.Dispose();
            DataAccumulator = null;
        }
    }

    public void Dispose() => DataAccumulator?.Dispose();
}
