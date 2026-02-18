namespace BlackFastProtocol.Package.Ack;

public sealed record AckPackageHandler : IBodyHandler<AckPackageBody>
{
    public async Task HandlePackageAsync(AckPackageBody package, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        if (context.LastSentPackage is { Header.Type: PackageType.Ack })
        {
            return;
        }
        
        context.LastReceivedPackage = package;
        
        var nextSequence = context.GetNextSequence();
        
        var header = new PackageHeader(context.SessionId, PackageType.Ack, nextSequence);
        var ack = new AckPackageBody();
        var responsePackage = new ProtocolPackage(header, ack);

        await context.Session.SendAsync(responsePackage, cancellationToken);
    }

    public void HandlePackage(AckPackageBody package, FastBlackSessionContext context)
    {
        if (context.LastSentPackage is { Header.Type: PackageType.Ack })
        {
            return;
        }
        
        context.LastReceivedPackage = package;
        
        var nextSequence = context.GetNextSequence();
        
        var header = new PackageHeader(context.SessionId, PackageType.Ack, nextSequence);
        var ack = new AckPackageBody();
        var responsePackage = new ProtocolPackage(header, ack);

        context.Session.Send(responsePackage);
    }
}