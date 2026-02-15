using System.Buffers.Binary;

namespace BlackFastProtocol.Package.DataHeader;

public sealed record DataHeaderPackage(int Id, int DataLength)
    : PackageBase(PackageType.DataHeader,
        Id,
        sizeof(PackageType) + sizeof(int) + sizeof(int)),IWriteablePackage, IReadablePackage<DataHeaderPackage>
{
    public int ToBytes(Span<byte> buffer)
    {
        if (buffer.Length < Length)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        buffer[0] = (byte)Type;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(1, 4), Id);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(5, 4), DataLength);
        return Length;
    }

    public static DataHeaderPackage ReadPackage(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < 9)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var span = buffer.Span;

        var id = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(1, 4));
        var dataLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(5, 4));
        return new DataHeaderPackage(id, dataLength);
    }
}