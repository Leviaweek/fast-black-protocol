namespace BlackFastProtocol.Package;

public interface IBodyHandler<in T> where T : class, IPackageBody
{
    public Task HandlePackageAsync(T package, FastBlackSessionContext context, CancellationToken cancellationToken);
    public void HandlePackage(T package, FastBlackSessionContext context);
}

public interface IBodyHandler
{
    public Task HandlePackageAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken);
}