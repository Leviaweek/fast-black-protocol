using BlackFastProtocol.Package.Ack;

namespace BlackFastProtocol.Package.DataPackage;

internal class DataPackageBodyHandler: IBodyHandler<DataPackageBody>
{
    public async Task HandlePackageAsync(PackageHeader header, DataPackageBody package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        if (context.ClientState is not StreamClientState streamClientState)
        {
            return;
        }

        if (streamClientState.DataAccumulator is null)
        {
            await context.DataChannel.Writer.WriteAsync(package.Data.ToArray(), cancellationToken);
            var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Ack);
            var ack = new AckPackageBody(header.Sequence);
            var responsePackage = new ProtocolPackage(responseHeader, ack);
            await context.Session.SendAsync(responsePackage, cancellationToken);
        }
        else
            streamClientState.DataAccumulator.TryAdd(header.Sequence, package.Data.Span);
    }

    public void HandlePackage(PackageHeader header, DataPackageBody package, FastBlackSessionContext context)
    {
        if (context.ClientState is not StreamClientState streamClientState)
        {
            return;
        }

        if (streamClientState.DataAccumulator is null)
        {
            context.DataChannel.Writer.TryWrite(package.Data.ToArray());
        }
        else
            streamClientState.DataAccumulator.TryAdd(header.Sequence, package.Data.Span);
    }
}