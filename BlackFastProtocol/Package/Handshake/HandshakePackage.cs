using System.Buffers.Binary;

namespace BlackFastProtocol.Package.Handshake;

public sealed record HandshakePackage : PackageBase, IWriteablePackage,
        IReadablePackage<HandshakePackage>
{
    public HandshakePackage(Guid sessionId, int id) : base(new PackageHeader(sessionId, PackageType.Handshake, id)) { }
    private HandshakePackage(PackageHeader header) : base(header) { }

    public int ToBytes(Span<byte> buffer)
    {
        if (buffer.Length < Length)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        Header.ToBytes(buffer);
        
        return Length;
    }

    public static HandshakePackage ReadPackage(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < 21)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var header = PackageHeader.ReadPackage(buffer);
        
        return new HandshakePackage(header);
    }
    
    public override int Length => Header.Length;
}