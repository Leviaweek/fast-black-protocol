using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.Package.Nack;

internal sealed class NackBodyHandler: IBodyHandler<Nack>
{
    public ValueTask<bool> TryHandlePackageAsync(PackageHeader header, Nack package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        context.AckAwaiter?.TrySetResult(package);
        return ValueTask.FromResult(true);
    }

    public bool TryHandlePackage(PackageHeader header, Nack package, FastBlackSessionContext context)
    {
        context.AckAwaiter?.TrySetResult(package);
        return true;
    }
}