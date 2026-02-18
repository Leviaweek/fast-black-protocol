namespace BlackFastProtocol.Package.Handshake;

public sealed class HandshakeBodyHandler: IBodyHandler<HandshakeBody>
{
    public async Task HandlePackageAsync(HandshakeBody package, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Received handshake from {context.Session.EndPoint}");

        context.IsHandshake = true;
        context.LastReceivedPackage = package;
        
        var nextSequence = context.GetNextSequence();
        var header = new PackageHeader(context.SessionId, PackageType.Handshake, nextSequence);
        var handshakeResponse = new HandshakeBody();
        var responsePackage = new ProtocolPackage(header, handshakeResponse);
        await context.Session.SendAsync(responsePackage, cancellationToken);
    }

    public void HandlePackage(HandshakeBody package, FastBlackSessionContext context)
    {
        Console.WriteLine($"Received handshake from {context.Session.EndPoint}");
        
        context.IsHandshake = true;

        var nextSequence = context.GetNextSequence();
        var header = new PackageHeader(context.SessionId, PackageType.Handshake, nextSequence);
        var handshakeResponse = new HandshakeBody();
        var responsePackage = new ProtocolPackage(header, handshakeResponse);
        context.Session.Send(responsePackage);
    }
}