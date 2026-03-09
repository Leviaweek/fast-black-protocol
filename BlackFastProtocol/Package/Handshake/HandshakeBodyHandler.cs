namespace BlackFastProtocol.Package.Handshake;

public sealed class HandshakeBodyHandler: IBodyHandler<HandshakeBody>
{
    public async Task HandlePackageAsync(PackageHeader header, HandshakeBody package, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Received handshake from {context.Session.EndPoint}");
        context.LastReceivedPackage = package;

        if (context.LastSentPackage is { Header.Type: PackageType.Handshake })
        {
            return;
        }
        
        context.IsHandshake = true;
        
        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Handshake);
        var handshakeResponse = new HandshakeBody();
        var responsePackage = new ProtocolPackage(responseHeader, handshakeResponse);
        await context.Session.SendAsync(responsePackage, cancellationToken);
    }

    public void HandlePackage(PackageHeader header, HandshakeBody package, FastBlackSessionContext context)
    {
        Console.WriteLine($"Received handshake from {context.Session.EndPoint}");
        
        context.IsHandshake = true;
        
        var responseHeader = PackageHeader.CreateFromContext(context, PackageType.Handshake);
        var handshakeResponse = new HandshakeBody();
        var responsePackage = new ProtocolPackage(responseHeader, handshakeResponse);
        context.Session.Send(responsePackage);
    }
}