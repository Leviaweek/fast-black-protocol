using UdpListener;

namespace UdpServer.Packages.Ack;

public sealed record AckPackageHandler : IPackageHandler<AckPackage>
{
    public async Task HandlePackageAsync(AckPackage package, UdpSessionContext context, CancellationToken cancellationToken)
    {
        if (context.LastReceivedPackage is { Type: PackageType.Ack })
        {
            return;
        }
        
        context.LastReceivedPackage = package;
        var responsePackage = new AckPackage(package.Id + 1);
        var buffer = new byte[responsePackage.Length];
        responsePackage.ToBytes(buffer);
        await context.Session.SendAsync(buffer, cancellationToken);
    }

    public void HandlePackage(AckPackage package, UdpSessionContext context)
    {
        if (context.LastReceivedPackage is { Type: PackageType.Ack })
        {
            return;
        }
        
        context.LastReceivedPackage = package;
        var responsePackage = new AckPackage(package.Id + 1);
        Span<byte> buffer = stackalloc byte[responsePackage.Length];
        responsePackage.ToBytes(buffer);
        context.Session.Send(buffer);
    }
}