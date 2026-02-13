using UdpListener;

namespace UdpServer.Packages.Handshake;

public sealed class HandshakePackageHandler: IPackageHandler<HandshakePackage>
{
    public async Task HandlePackageAsync(HandshakePackage package, UdpSessionContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Received handshake from {context.Session.EndPoint} with id {package.Id}");
        
        var handshakeResponse = new HandshakePackage(package.Id + 1);
        var buffer = new byte[handshakeResponse.Length];
        handshakeResponse.ToBytes(buffer);
        await context.Session.SendAsync(buffer, cancellationToken);
    }

    public void HandlePackage(HandshakePackage package, UdpSessionContext context)
    {
        Console.WriteLine($"Received handshake from {context.Session.EndPoint} with id {package.Id}");
        
        var handshakeResponse = new HandshakePackage(package.Id + 1);
        Span<byte> buffer = stackalloc byte[handshakeResponse.Length];
        handshakeResponse.ToBytes(buffer);
        context.Session.Send(buffer);
    }
}