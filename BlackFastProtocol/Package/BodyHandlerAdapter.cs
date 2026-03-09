using System.Runtime.CompilerServices;

namespace BlackFastProtocol.Package;

public sealed class BodyHandlerAdapter<T>(IBodyHandler<T> innerHandler) : IBodyHandler
    where T : class, IReadableData<T>, IPackageBody
{
    public async Task HandlePackageAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        var packageBody = Unsafe.As<T>(package.Body);

        await innerHandler.HandlePackageAsync(package.Header, packageBody, context, cancellationToken);
    }
}