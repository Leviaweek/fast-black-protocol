using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.State;

internal abstract class ClientState
{
    public abstract Task HandleAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken);
}