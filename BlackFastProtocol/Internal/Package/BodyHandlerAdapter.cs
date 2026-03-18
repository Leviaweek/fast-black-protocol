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

        await innerHandler.HandlePackageAsync(package.Header, packageBody, context, cancellationToken);
    }
}