using UdpListener;

namespace UdpServer.Packages;

public sealed class PackageHandlerAdapter<T>(IPackageHandler<T> innerHandler) : IPackageHandler
    where T : Package, ITypedPackage, IReadablePackage<T>
{
    public async Task HandlePackageAsync(ReadOnlyMemory<byte> buffer, UdpSessionContext context, CancellationToken cancellationToken)
    {
        var package = PackageRw.ReadPackage<T>(buffer);
        await innerHandler.HandlePackageAsync(package, context, cancellationToken);
    }
}