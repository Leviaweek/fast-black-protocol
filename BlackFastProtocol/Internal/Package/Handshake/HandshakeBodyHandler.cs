using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;
using BlackFastProtocol.Internal.State;

namespace BlackFastProtocol.Internal.Package.Handshake;

internal sealed class HandshakeBodyHandler : IBodyHandler<HandshakeBody>
{
    public async ValueTask<bool> TryHandlePackageAsync(PackageHeader header, HandshakeBody package,
        FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        context.Info.IsHandshake = true;

        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Handshake);
        var responsePackage = new ProtocolPackage(responseHeader, new HandshakeBody());
        await context.Session.SendAsync(responsePackage, cancellationToken);

        // Pass DataChannel.Writer so StreamClientState can inject DataAccumulator correctly.
        context.ClientState = new StreamClientState(context.DataChannel.Writer);
        return true;
    }

    public bool TryHandlePackage(PackageHeader header, HandshakeBody package, FastBlackSessionContext context)
    {
        context.Info.IsHandshake = true;

        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Handshake);
        context.Session.Send(new ProtocolPackage(responseHeader, new HandshakeBody()));
        context.ClientState = new StreamClientState(context.DataChannel.Writer);
        return true;
    }
}
