using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.Package.Interfaces;

internal interface IBodyHandler<in T> where T : class, IPackageBody
{
    public Task HandlePackageAsync(PackageHeader header, T package, FastBlackSessionContext context, CancellationToken cancellationToken);
    public void HandlePackage(PackageHeader header, T package, FastBlackSessionContext context);
}

internal interface IBodyHandler
{
    public Task HandlePackageAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken);
}