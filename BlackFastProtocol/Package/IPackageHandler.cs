using BlackFastProtocol;

namespace UdpServer.Packages;

public interface IPackageHandler<in T> where T : Package, ITypedPackage, IReadablePackage<T>
{
    public Task HandlePackageAsync(T package, FastBlackSessionConext context, CancellationToken cancellationToken);
    public void HandlePackage(T package, FastBlackSessionConext context);
}

public interface IPackageHandler
{
    public Task HandlePackageAsync(ReadOnlyMemory<byte> buffer, FastBlackSessionConext context,
        CancellationToken cancellationToken);
}
