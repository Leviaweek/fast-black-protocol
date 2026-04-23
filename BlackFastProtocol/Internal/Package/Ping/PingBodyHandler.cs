using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Package.Pong;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.Package.Ping;

internal sealed class PingBodyHandler: IBodyHandler<PingBody>
{
    public async ValueTask<bool> TryHandlePackageAsync(PackageHeader header, PingBody package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Pong);
        var responsePackage = new ProtocolPackage(responseHeader, new PongBody());
        await context.Session.SendAsync(responsePackage, cancellationToken);
        return true;
    }

    public bool TryHandlePackage(PackageHeader header, PingBody package, FastBlackSessionContext context)
    {
        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Pong);
        var responsePackage = new ProtocolPackage(responseHeader, new PongBody());
        context.Session.Send(responsePackage);
        return true;
    }
}