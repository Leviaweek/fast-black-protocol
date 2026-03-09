namespace BlackFastProtocol.Package;

public interface IBodyHandler<in T> where T : class, IPackageBody
{
    public Task HandlePackageAsync(PackageHeader header, T package, FastBlackSessionContext context, CancellationToken cancellationToken);
    public void HandlePackage(PackageHeader header, T package, FastBlackSessionContext context);
}

public interface IBodyHandler
{
    public Task HandlePackageAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken);
}