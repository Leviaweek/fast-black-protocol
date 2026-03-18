using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.Package.Interfaces;

internal interface IBodyHandler<in T> where T : class, IPackageBody
{
    public Task<bool> TryHandlePackageAsync(PackageHeader header, T package, FastBlackSessionContext context, CancellationToken cancellationToken);
    public bool TryHandlePackage(PackageHeader header, T package, FastBlackSessionContext context);
}

internal interface IBodyHandler
{
    public Task HandlePackageAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken);
}