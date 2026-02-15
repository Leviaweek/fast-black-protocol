using System.Buffers.Binary;

namespace BlackFastProtocol.Package.Handshake;

public sealed record HandshakePackage(int Id)
    : PackageBase(PackageType.Handshake, Id, sizeof(PackageType) + sizeof(int)), IWriteablePackage,
        IReadablePackage<HandshakePackage>
{
    public int ToBytes(Span<byte> buffer)
    {
        if (buffer.Length < Length)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        buffer[0] = (byte)Type;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(1, 4), Id);
        return Length;
    }

    public static HandshakePackage ReadPackage(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < 5)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var span = buffer.Span;
        
        var id = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(1, 4));
        return new HandshakePackage(id);
    }
}