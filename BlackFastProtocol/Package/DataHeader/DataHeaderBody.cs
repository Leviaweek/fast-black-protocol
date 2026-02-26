using System.Buffers.Binary;

namespace BlackFastProtocol.Package.DataHeader;

public sealed record DataHeaderBody(int DataLength) : IPackageBody, IReadableData<DataHeaderBody>
{
    public int WriteData(Span<byte> buffer, int offset = 0)
    {
        if (buffer.Length < Length + offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset, 4), DataLength);
        
        return Length;
    }

    public static DataHeaderBody ReadData(ReadOnlyMemory<byte> buffer, int offset = 0)
    {
        if (buffer.Length < offset + 4)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var span = buffer.Span;

        var dataLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
        
        return new DataHeaderBody(dataLength);
    }

    public int Length => sizeof(int);
}