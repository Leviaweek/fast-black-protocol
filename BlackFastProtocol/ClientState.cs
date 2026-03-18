using BlackFastProtocol.Package;

namespace BlackFastProtocol;

internal abstract class ClientState
{
    public abstract Task HandleAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken);
}