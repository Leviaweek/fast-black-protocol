namespace BlackFastProtocol.Package.Handshake;

public sealed class HandshakePackageHandler: IPackageHandler<HandshakePackage>
{
    public async Task HandlePackageAsync(HandshakePackage package, FastBlackSessionContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Received handshake from {context.Session.EndPoint} with id {package.Id}");

        context.IsHandshaked = true;
        
        var handshakeResponse = new HandshakePackage(package.Id + 1);
        var buffer = new byte[handshakeResponse.Length];
        handshakeResponse.ToBytes(buffer);
        await context.Session.SendAsync(buffer, cancellationToken);
    }

    public void HandlePackage(HandshakePackage package, FastBlackSessionContext context)
    {
        Console.WriteLine($"Received handshake from {context.Session.EndPoint} with id {package.Id}");
        
        context.IsHandshaked = true;

        var handshakeResponse = new HandshakePackage(package.Id + 1);
        Span<byte> buffer = stackalloc byte[handshakeResponse.Length];
        handshakeResponse.ToBytes(buffer);
        context.Session.Send(buffer);
    }
}