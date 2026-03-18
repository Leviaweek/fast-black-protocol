using System.Buffers.Binary;
using BlackFastProtocol.Internal.Package.Interfaces;

namespace BlackFastProtocol.Internal.Package.Ack;

internal sealed record AckPackageBody(uint LastReadSequence) : IPackageBody, IReadableData<AckPackageBody>
{
    public int WriteData(Span<byte> buffer, int offset = 0)
    {   
        if (buffer.Length < Length + offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, 4), LastReadSequence);
        
        return Length;
    }

    public static AckPackageBody ReadData(ReadOnlyMemory<byte> buffer, int offset = 0)
    {
        if (buffer.Length < 4 + offset)
            throw new ArgumentException("Buffer too small", nameof(buffer));
        
        var lastReadSequence = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Span.Slice(offset, 4));
        
        return new AckPackageBody(lastReadSequence);
    }

    public int Length => 0;
}