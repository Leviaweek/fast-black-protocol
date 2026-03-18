using BlackFastProtocol.Internal.Package;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.State;

internal sealed class DefaultClientState : ClientState
{
    public override async Task HandleAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        context.SequenceManager.AdvanceExpected();
        await PackageHelper.Handlers[package.Header.Type].HandlePackageAsync(package, context, cancellationToken);
    }
}