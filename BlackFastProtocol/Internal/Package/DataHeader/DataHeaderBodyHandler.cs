using BlackFastProtocol.Internal.Buffer;
using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;
using BlackFastProtocol.Internal.State;

namespace BlackFastProtocol.Internal.Package.DataHeader;

internal sealed class DataHeaderBodyHandler : IBodyHandler<DataHeaderBody>
{
    public ValueTask<bool> TryHandlePackageAsync(PackageHeader header, DataHeaderBody package,
        FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        if (!context.Info.IsHandshake)
            return ValueTask.FromResult(false);

        if (context.ClientState is StreamClientState streamClientState)
        {
            if (streamClientState.DataAccumulator is null)
            {
                // First transfer on this stream state — allocate accumulator.
                // BUG-5 FIX: pass context.SequenceManager.Expected so DataWindow
                // starts at the correct incoming sequence, not always 0.
                streamClientState.DataAccumulator = new DataAccumulator(
                    package.DataLength,
                    context.DataChannel.Writer,
                    context.SequenceManager.Expected);
                return ValueTask.FromResult(true);
            }

            if (!streamClientState.DataAccumulator.IsComplete())
                return ValueTask.FromResult(false);

            // Reuse existing accumulator (avoids allocation) for back-to-back transfers.
            streamClientState.DataAccumulator.Reload(
                context.SequenceManager.Expected, (uint)package.DataLength);
            return ValueTask.FromResult(true);
        }

        // BUG-5 FIX: same fix for the path that creates a new StreamClientState.
        // StreamClientState's constructor will call new DataAccumulator(length, writer, startSeq=0)
        // via DataAccumulator's default startSequence parameter — but here we need the real
        // current sequence, so we create the accumulator explicitly.
        var state = new StreamClientState(context.DataChannel.Writer);
        state.DataAccumulator = new DataAccumulator(
            package.DataLength,
            context.DataChannel.Writer,
            context.SequenceManager.Expected);
        context.ClientState = state;
        return ValueTask.FromResult(true);
    }

    public bool TryHandlePackage(PackageHeader header, DataHeaderBody package, FastBlackSessionContext context)
    {
        if (!context.Info.IsHandshake)
            return false;

        // BUG-5 FIX: pass current expected sequence.
        var state = new StreamClientState(context.DataChannel.Writer);
        state.DataAccumulator = new DataAccumulator(
            package.DataLength,
            context.DataChannel.Writer,
            context.SequenceManager.Expected);
        context.ClientState = state;
        return true;
    }
}
