namespace BlackFastProtocol.Package.Ack;

public sealed record AckPackageHandler : IPackageHandler<AckPackage>
{
    public async Task HandlePackageAsync(AckPackage package, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        if (context.LastReceivedPackage is { Type: PackageType.Ack })
        {
            return;
        }
        
        context.LastReceivedPackage = package;
        var responsePackage = new AckPackage(package.Id + 1);
        await context.Session.SendAsync(responsePackage, cancellationToken);
    }

    public void HandlePackage(AckPackage package, FastBlackSessionContext context)
    {
        if (context.LastReceivedPackage is { Type: PackageType.Ack })
        {
            return;
        }
        
        context.LastReceivedPackage = package;
        var responsePackage = new AckPackage(package.Id + 1);
        context.Session.Send(responsePackage);
    }
}