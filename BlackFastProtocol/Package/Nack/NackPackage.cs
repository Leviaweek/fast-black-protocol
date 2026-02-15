using System.Buffers.Binary;

namespace BlackFastProtocol.Package.Nack;

public sealed record NackPackage(int Id, ReadOnlyMemory<int> LostIds)
    : PackageBase(PackageType.UnAck, Id, sizeof(PackageType) + sizeof(int) + LostIds.Length * sizeof(int)),
        IWriteablePackage, IReadablePackage<NackPackage>
{
    
    public int ToBytes(Span<byte> buffer)
    {
        if (buffer.Length < Length)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        buffer[0] = (byte)Type;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(1, 4), Id);
        for (var i = 0; i < LostIds.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(5 + i * 4, 4), LostIds.Span[i]);
        }

        return Length;
    }
    
    public static NackPackage ReadPackage(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < 5)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var span = buffer.Span;
        
        var id = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(1, 4));
        var length = (buffer.Length - 5) / 4;
        var lostIds = new int[length];
        for (var i = 0; i < length; i++)
        {
            lostIds[i] = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(5 + i * 4, 4));
        }
        return new NackPackage(id, lostIds);
    }
}