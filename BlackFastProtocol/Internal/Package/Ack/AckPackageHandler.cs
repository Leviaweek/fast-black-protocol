using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.Package.Ack;

internal sealed record AckPackageHandler : IBodyHandler<AckPackageBody>
{
    public async ValueTask<bool> TryHandlePackageAsync(PackageHeader header, AckPackageBody package, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        if (!context.Info.IsHandshake)
        {
            return false;
        }
        
        if (context.Tracker.LastSentPackage is { Header.Type: PackageType.Ack })
        {
            return false;
        }
        
        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Ack);
        var ack = new AckPackageBody(header.Sequence);
        var responsePackage = new ProtocolPackage(responseHeader, ack);

        await context.Session.SendAsync(responsePackage, cancellationToken);
        return true;
    }

    public bool TryHandlePackage(PackageHeader header, AckPackageBody package, FastBlackSessionContext context)
    {
        if (context.Tracker.LastSentPackage is { Header.Type: PackageType.Ack })
        {
            return false;
        }
        
        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Ack);
        var ack = new AckPackageBody(header.Sequence);
        var responsePackage = new ProtocolPackage(responseHeader, ack);

        context.Session.Send(responsePackage);
        return true;
    }
}