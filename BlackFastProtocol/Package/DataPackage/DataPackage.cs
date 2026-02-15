using System.ComponentModel.DataAnnotations;

namespace BlackFastProtocol.Package.DataPackage;

public sealed record DataPackage(int Id, [MaxLength(1024)]ReadOnlyMemory<byte> Data) : PackageBase(PackageType.DataPackage, Id, sizeof(PackageType) + sizeof(int) + Data.Length),
    IWriteablePackage, IReadablePackage<DataPackage>
{
    public int ToBytes(Span<byte> buffer)
    {
        if (buffer.Length < Length)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        buffer[0] = (byte)Type;
        BitConverter.TryWriteBytes(buffer.Slice(1, 4), Id);
        Data.Span.CopyTo(buffer.Slice(5, Data.Length));
        return Length;
    }

    public static DataPackage ReadPackage(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < 5)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var span = buffer.Span;
        
        var id = BitConverter.ToInt32(span.Slice(1, 4));
        var data = buffer[5..];
        return new DataPackage(id, data);
    }
}