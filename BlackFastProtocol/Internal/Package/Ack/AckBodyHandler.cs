using BlackFastProtocol.Internal.Package.Interfaces;
using BlackFastProtocol.Internal.Session;

namespace BlackFastProtocol.Internal.Package.Ack;

/// <summary>
/// Routes incoming ACK into the session's SendEngine.
/// Does NOT complete any TaskCompletionSource — the send pipeline is now event-driven.
/// </summary>
internal sealed record AckBodyHandler : IBodyHandler<AckBody>
{
    public async ValueTask<bool> TryHandlePackageAsync(PackageHeader header, AckBody package,
        FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        // Fire-and-forget into the send engine; errors are swallowed intentionally here
        // because a single bad ACK must not crash the receive loop.
        await context.SendEngine.OnAckAsync(package, cancellationToken);
        return true;
    }

    public bool TryHandlePackage(PackageHeader header, AckBody package, FastBlackSessionContext context)
    {
        _ = context.SendEngine.OnAckAsync(package, CancellationToken.None);
        return true;
    }
}
