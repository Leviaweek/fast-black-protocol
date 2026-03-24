using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.Package.Ack;

internal sealed record AckBodyHandler : IBodyHandler<AckBody>
{
    public ValueTask<bool> TryHandlePackageAsync(PackageHeader header, AckBody package, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        context.AckAwaiter?.TrySetResult(package);
        return ValueTask.FromResult(true);
    }

    public bool TryHandlePackage(PackageHeader header, AckBody package, FastBlackSessionContext context)
    {
        context.AckAwaiter?.TrySetResult(package);
        return true;
    }
}