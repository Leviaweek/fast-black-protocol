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

        if (package.DataLength <= 0)
            return ValueTask.FromResult(false);

        if (context.ClientState is StreamClientState streamClientState)
        {
            if (streamClientState.DataAccumulator is null)
            {
                streamClientState.DataAccumulator = new DataAccumulator(
                    package.DataLength,
                    context.DataChannel.Writer,
                    context.SequenceManager.Expected);
                return ValueTask.FromResult(true);
            }

            if (!streamClientState.DataAccumulator.IsComplete())
                return ValueTask.FromResult(false);

            streamClientState.DataAccumulator.Reload(
                context.SequenceManager.Expected, (uint)package.DataLength);
            return ValueTask.FromResult(true);
        }

        var state = new StreamClientState();
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

        if (package.DataLength <= 0)
            return false;

        var state = new StreamClientState();
        state.DataAccumulator = new DataAccumulator(
            package.DataLength,
            context.DataChannel.Writer,
            context.SequenceManager.Expected);
        context.ClientState = state;
        return true;
    }
}
