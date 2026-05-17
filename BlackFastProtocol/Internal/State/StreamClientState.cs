using BlackFastProtocol.Internal.Buffer;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Ack;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.State;

internal sealed class StreamClientState : ClientState, IDisposable
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

        if (!DataAccumulator.Window.HasData) return;

        var (baseSequence, receivedMask) = DataAccumulator.Window.CreateAck();
        var isWindowReady = DataAccumulator.Window.IsReady();

        if (isWindowReady)
            DataAccumulator.FlushWindow();

        await context.SendAckAsync(baseSequence, receivedMask, cancellationToken);

        if (!isWindowReady) return;

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
