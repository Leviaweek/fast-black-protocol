
using System.Buffers.Binary;

namespace BlackFastProtocol.Package.DataFrame;

public sealed record DataFramePackage(int Id, ReadOnlyMemory<byte> Data)
    : PackageBase(PackageType.DataFrame, Id, sizeof(PackageType) + sizeof(int) + Data.Length), IWriteablePackage,
        IReadablePackage<DataFramePackage>
{
    public int ToBytes(Span<byte> buffer)
    {
        if (buffer.Length < Length)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        buffer[0] = (byte)Type;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(1, 4), Id);
        Data.Span.CopyTo(buffer.Slice(5, Data.Length));
        return Length;
    }

    public static DataFramePackage ReadPackage(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < 5)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var span = buffer.Span;
        
        var id = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(1, 4));
        var data = buffer[5..];
        return new DataFramePackage(id, data);
    }
}