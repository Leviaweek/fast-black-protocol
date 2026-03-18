using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;
using BlackFastProtocol.Internal.State;

namespace BlackFastProtocol.Internal.Package.DataHeader;

internal sealed class DataHeaderBodyHandler : IBodyHandler<DataHeaderBody>
{
    public ValueTask<bool> TryHandlePackageAsync(PackageHeader header, DataHeaderBody package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Info.IsHandshake)
            return ValueTask.FromResult(false);

        context.ClientState = new StreamClientState(package.DataLength);
        return ValueTask.FromResult(true);
    }

    public bool TryHandlePackage(PackageHeader header, DataHeaderBody package, FastBlackSessionContext context)
    {
        if (!context.Info.IsHandshake)
        {
            return false;
        }
    
        context.ClientState = new StreamClientState(package.DataLength);
        return true;
    }
}