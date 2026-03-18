namespace BlackFastProtocol.Package.Ack;

internal sealed record AckPackageHandler : IBodyHandler<AckPackageBody>
{
    public async Task HandlePackageAsync(PackageHeader header, AckPackageBody package, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        if (!context.Info.IsHandshake)
        {
            return;
        }
        
        if (context.Tracker.LastSentPackage is { Header.Type: PackageType.Ack })
        {
            return;
        }
        
        context.Tracker.LastReceivedPackage = package;
        
        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Ack);
        var ack = new AckPackageBody(header.Sequence);
        var responsePackage = new ProtocolPackage(responseHeader, ack);

        await context.Session.SendAsync(responsePackage, cancellationToken);
    }

    public void HandlePackage(PackageHeader header, AckPackageBody package, FastBlackSessionContext context)
    {
        if (context.Tracker.LastSentPackage is { Header.Type: PackageType.Ack })
        {
            return;
        }
        
        context.Tracker.LastReceivedPackage = package;
        
        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Ack);
        var ack = new AckPackageBody(header.Sequence);
        var responsePackage = new ProtocolPackage(responseHeader, ack);

        context.Session.Send(responsePackage);
    }
}