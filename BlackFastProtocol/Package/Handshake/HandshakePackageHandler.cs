namespace BlackFastProtocol.Package.Handshake;

public sealed class HandshakePackageHandler: IPackageHandler<HandshakePackage>
{
    public async Task HandlePackageAsync(HandshakePackage package, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Received handshake from {context.Session.EndPoint} with id {package.Header.Id}");

        context.IsHandshake = true;
        
        var nextSequence = context.GetNextSequence();
        var handshakeResponse = new HandshakePackage(context.SessionId, nextSequence);
        await context.Session.SendAsync(handshakeResponse, cancellationToken);
    }

    public void HandlePackage(HandshakePackage package, FastBlackSessionContext context)
    {
        Console.WriteLine($"Received handshake from {context.Session.EndPoint} with id {package.Header.Id}");
        
        context.IsHandshake = true;

        var nextSequence = context.GetNextSequence();
        var handshakeResponse = new HandshakePackage(context.SessionId, nextSequence);
        context.Session.Send(handshakeResponse);
    }
}