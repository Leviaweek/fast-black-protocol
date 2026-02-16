using System.ComponentModel.DataAnnotations;

namespace BlackFastProtocol.Package.DataPackage;

public sealed record DataPackage : PackageBase,
    IWriteablePackage, IReadablePackage<DataPackage>
{
    public DataPackage(Guid sessionId, int id, [MaxLength(1024)] ReadOnlyMemory<byte> data) : base(sessionId,
        PackageType.DataPackage, id)
    {
        Data = data;
    }
    private DataPackage(PackageHeader header, ReadOnlyMemory<byte> data) : base(header)
    {
        Data = data;
    }

    public int ToBytes(Span<byte> buffer)
    {
        if (buffer.Length < Length)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        Header.ToBytes(buffer);
        
        Data.Span.CopyTo(buffer.Slice(Header.Length, Data.Length));
        return Length;
    }

    public static DataPackage ReadPackage(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < 5)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var span = buffer.Span;
        
        var header = PackageHeader.ReadPackage(buffer);
        
        var data = buffer[header.Length..].ToArray();
        return new DataPackage(header, data);
    }
    
    public ReadOnlyMemory<byte> Data { get; }

    public override int Length => Header.Length + Data.Length;
}