using BlackFastProtocol.Internal.Buffer;
using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Package.Ack;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.State;

internal sealed class StreamClientState(int dataLength = 0) : ClientState, IDisposable
{
    internal DataAccumulator? DataAccumulator { get; private set; } = dataLength > 0 ? new DataAccumulator(dataLength) : null;

    public override async ValueTask HandleAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        context.SequenceManager.AdvanceExpected();
        await PackageHelper.Handlers[package.Header.Type].HandlePackageAsync(package, context, cancellationToken);

        await PostProcessDataAsync(package, context, cancellationToken);
    }
    
    private async Task PostProcessDataAsync(ProtocolPackage package, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        if (DataAccumulator is null) return;

        if (!DataAccumulator.Window.IsReady()) return;
        
        context.DataChannel.Writer.TryWrite(DataAccumulator.FlushWindow());

        var header = PackageHeader.CreateFromContext(context, PackageType.Ack);
        var ack = new AckBody(package.Header.Sequence);
        
        var responsePackage = new ProtocolPackage(header, ack);

        await context.Session.SendAsync(responsePackage, cancellationToken);

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

    public void Dispose()
    {
        DataAccumulator?.Dispose();
    }
}