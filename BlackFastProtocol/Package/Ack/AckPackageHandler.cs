namespace BlackFastProtocol.Package.Ack;

public sealed record AckPackageHandler : IPackageHandler<AckPackage>
{
    public async Task HandlePackageAsync(AckPackage package, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        if (context.LastReceivedPackage is { Header.Type: PackageType.Ack })
        {
            return;
        }
        
        context.LastReceivedPackage = package;
        
        var nextSequence = context.GetNextSequence();
        
        var responsePackage = new AckPackage(context.SessionId, nextSequence);
        await context.Session.SendAsync(responsePackage, cancellationToken);
    }

    public void HandlePackage(AckPackage package, FastBlackSessionContext context)
    {
        if (context.LastReceivedPackage is { Header.Type: PackageType.Ack })
        {
            return;
        }
        
        context.LastReceivedPackage = package;
        
        var nextSequence = context.GetNextSequence();
        
        var responsePackage = new AckPackage(context.SessionId, nextSequence);
        context.Session.Send(responsePackage);
    }
}