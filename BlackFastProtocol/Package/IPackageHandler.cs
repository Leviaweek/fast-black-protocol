namespace BlackFastProtocol.Package;

public interface IPackageHandler<in T> where T : PackageBase, ITypedPackage, IReadablePackage<T>
{
    public Task HandlePackageAsync(T package, FastBlackSessionContext context, CancellationToken cancellationToken);
    public void HandlePackage(T package, FastBlackSessionContext context);
}

public interface IPackageHandler
{
    public Task HandlePackageAsync(ReadOnlyMemory<byte> buffer, FastBlackSessionContext context,
        CancellationToken cancellationToken);
}
