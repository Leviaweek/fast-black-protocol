namespace BlackFastProtocol.Package;

public sealed class PackageHandlerAdapter<T>(IPackageHandler<T> innerHandler) : IPackageHandler
    where T : IReadablePackage<T>
{
    public async Task HandlePackageAsync(ReadOnlyMemory<byte> buffer, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        var package = PackageRw.ReadPackage<T>(buffer);
        await innerHandler.HandlePackageAsync(package, context, cancellationToken);
    }
}