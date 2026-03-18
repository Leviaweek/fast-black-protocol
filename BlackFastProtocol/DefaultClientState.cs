using BlackFastProtocol.Package;

namespace BlackFastProtocol;

internal sealed class DefaultClientState : ClientState
{
    public override async Task HandleAsync(ProtocolPackage package, FastBlackSessionContext context,
        CancellationToken cancellationToken)
    {
        context.SequenceManager.AdvanceExpected();
        await PackageHelper.Handlers[package.Header.Type].HandlePackageAsync(package, context, cancellationToken);
    }
}