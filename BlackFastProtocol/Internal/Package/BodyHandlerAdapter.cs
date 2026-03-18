using System.Runtime.CompilerServices;
using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.Package;

internal sealed class BodyHandlerAdapter<T>(IBodyHandler<T> innerHandler) : IBodyHandler
    where T : class, IReadableData<T>, IPackageBody
{
    public async Task HandlePackageAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        var packageBody = Unsafe.As<T>(package.Body);

        var result = await innerHandler.TryHandlePackageAsync(package.Header, packageBody, context, cancellationToken);
        
        if (result)
            context.Tracker.LastReceivedPackage = package.Body;
    }
}