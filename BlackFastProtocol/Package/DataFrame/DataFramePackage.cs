
using System.Buffers.Binary;

namespace BlackFastProtocol.Package.DataFrame;

public sealed record DataFramePackage : PackageBase, IWriteablePackage,
        IReadablePackage<DataFramePackage>
{
    public DataFramePackage(Guid sessionId, int id, ReadOnlyMemory<byte> data) : base(sessionId, PackageType.DataFrame, id) { Data = data; }
    private DataFramePackage(PackageHeader header, ReadOnlyMemory<byte> data) : base(header) { Data = data; }

    public int ToBytes(Span<byte> buffer)
    {
        if (buffer.Length < Length)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        Header.ToBytes(buffer);
        
        Data.Span.CopyTo(buffer.Slice(Header.Length, Data.Length));
        return Length;
    }

    public static DataFramePackage ReadPackage(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < 5)
            throw new ArgumentException("Buffer too small", nameof(buffer));

        var header = PackageHeader.ReadPackage(buffer);
        
        var data = buffer[header.Length..].ToArray();
        
        return new DataFramePackage(header, data);
    }
    
    public ReadOnlyMemory<byte> Data { get; }
    public override int Length => Header.Length + Data.Length;
}