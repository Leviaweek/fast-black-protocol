using BlackFastProtocol.Internal.Package.Ack;
using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;
using BlackFastProtocol.Internal.State;

namespace BlackFastProtocol.Internal.Package.DataPackage;

internal sealed class DataPackageBodyHandler: IBodyHandler<DataPackageBody>
{
    public async ValueTask<bool> TryHandlePackageAsync(PackageHeader header, DataPackageBody package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        if (context.ClientState is not StreamClientState streamClientState)
        {
            return false;
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

        return true;
    }

    public bool TryHandlePackage(PackageHeader header, DataPackageBody package, FastBlackSessionContext context)
    {
        if (context.ClientState is not StreamClientState streamClientState)
        {
            return false;
        }

        if (streamClientState.DataAccumulator is null)
        {
            context.DataChannel.Writer.TryWrite(package.Data.ToArray());
        }
        else
            streamClientState.DataAccumulator.TryAdd(header.Sequence, package.Data.Span);

        return true;
    }
}