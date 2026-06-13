using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;
using BlackFastProtocol.Internal.State;
using BlackFastProtocol.Public;

namespace BlackFastProtocol.Internal.Package.Handshake;

internal sealed class HandshakeBodyHandler : IBodyHandler<HandshakeBody>
{
    public async ValueTask<bool> TryHandlePackageAsync(PackageHeader header, HandshakeBody package,
        FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        if (context.Info.IsHandshake && context.ClientState is not DefaultClientState)
        {
            if (context.Session is BlackFastServerClient)
            {
                var duplicateResponseHeader = PackageHeader.CreateFromContext(context, PackageType.Handshake);
                var duplicateResponse = new ProtocolPackage(duplicateResponseHeader, new HandshakeBody());
                await context.Session.SendAsync(duplicateResponse, cancellationToken);
            }

            return true;
        }

        if (context.ClientState is not DefaultClientState clientState)
            return false;

        context.ClientState = new StreamClientState();
            
        if (context.Info.IsHandshake)
        {
            clientState.Source.TrySetResult();
            return true;
        }
        
        context.Info.IsHandshake = true;

        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Handshake);
        var responsePackage = new ProtocolPackage(responseHeader, new HandshakeBody());
        await context.Session.SendAsync(responsePackage, cancellationToken);

        context.ClientState = new StreamClientState();
        return true;
    }

    public bool TryHandlePackage(PackageHeader header, HandshakeBody package, FastBlackSessionContext context)
    {
        context.Info.IsHandshake = true;

        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Handshake);
        context.Session.Send(new ProtocolPackage(responseHeader, new HandshakeBody()));
        context.ClientState = new StreamClientState();
        return true;
    }
}
