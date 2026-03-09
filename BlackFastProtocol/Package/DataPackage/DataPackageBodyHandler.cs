namespace BlackFastProtocol.Package.DataPackage;

public class DataPackageBodyHandler: IBodyHandler<DataPackageBody>
{
    public async Task HandlePackageAsync(PackageHeader header, DataPackageBody package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        
    }

    public void HandlePackage(PackageHeader header, DataPackageBody package, FastBlackSessionContext context)
    {
        
    }
}