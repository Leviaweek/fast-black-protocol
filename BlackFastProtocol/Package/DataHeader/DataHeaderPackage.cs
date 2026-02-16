using System.Buffers.Binary;

namespace BlackFastProtocol.Package.DataHeader;

public sealed record DataHeaderPackage : PackageBase,IWriteablePackage, IReadablePackage<DataHeaderPackage>
{
    public DataHeaderPackage(Guid sessionId, int id, int dataLength) : base(sessionId, PackageType.DataHeader, id) { DataLength = dataLength; }
    private DataHeaderPackage(PackageHeader header, int dataLength) : base(header) { DataLength = dataLength; }

    public int ToBytes(Span<byte> buffer)
    {
        if (buffer.Length < Length)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        Header.ToBytes(buffer);
        
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(Header.Length, 4), DataLength);
        
        return Length;
    }

    public static DataHeaderPackage ReadPackage(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.Length < 9)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var span = buffer.Span;

        var header = PackageHeader.ReadPackage(buffer);
        
        var dataLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(header.Length, 4));
        
        return new DataHeaderPackage(header, dataLength);
    }
    public int DataLength { get; }
    public override int Length => Header.Length + sizeof(int);
}