using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;
using BlackFastProtocol.Internal.State;

namespace BlackFastProtocol.Internal.Package.Handshake;

internal sealed class HandshakeBodyHandler: IBodyHandler<HandshakeBody>
{
    public async Task<bool> TryHandlePackageAsync(PackageHeader header, HandshakeBody package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        context.Info.IsHandshake = true;
        
        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Handshake);
        var handshakeResponse = new HandshakeBody();
        var responsePackage = new ProtocolPackage(responseHeader, handshakeResponse);
        await context.Session.SendAsync(responsePackage, cancellationToken);
        context.ClientState = new StreamClientState();
        return true;
    }

    public bool TryHandlePackage(PackageHeader header, HandshakeBody package, FastBlackSessionContext context)
    {
        context.Info.IsHandshake = true;
        
        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Handshake);
        var handshakeResponse = new HandshakeBody();
        var responsePackage = new ProtocolPackage(responseHeader, handshakeResponse);
        context.Session.Send(responsePackage);
        context.ClientState = new StreamClientState();
        
        return true;
    }
}